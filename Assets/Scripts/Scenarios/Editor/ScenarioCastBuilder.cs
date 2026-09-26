using System.Collections.Generic;
using System.IO;
using Game.Characters.Face;
using Game.Characters.Passengers;
using Game.Scenarios.Presentation;
using Game.Scenarios.Presentation.UI;
using Game.Scenarios.Presentation.World;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VSM.Interaction;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Places a scenario's cast in the open scene from a *.cast.json file: passengers on fixed seats with actor ids,
    /// emotions, world-interaction targets, placeholder props (cap, scarf, thermos) and the scenario starter.
    /// Running it again replaces the previous cast. Requires the scenario UI (ScenarioRunner) in the scene.
    /// </summary>
    public static class ScenarioCastBuilder
    {
        private const string UnattendedItemCast = "Assets/Data/Scenarios/Casts/UnattendedItem.cast.json";
        private const string CastRootName = "ScenarioCast";
        private const string MaterialsFolder = "Assets/Data/Scenarios/Materials";
        private const string MalePrefabPath = "Assets/Prefabs/Passengers/Passenger_Male.prefab";
        private const string FemalePrefabPath = "Assets/Prefabs/Passengers/Passenger_Female.prefab";
        private const string ExpressionsFolder = "Assets/Data/Characters/FaceExpressions";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private static readonly Vector3 MarkerOffset = new Vector3(0f, 1.55f, 0f);
        private static readonly Vector3 ObjectMarkerOffset = new Vector3(0f, 0.6f, 0f);
        private static readonly Color StartMarkerColor = new Color(0.35f, 0.9f, 0.45f, 1f);
        private static readonly Color TargetMarkerColor = new Color(0.95f, 0.3f, 0.2f, 1f);

        private static readonly (string Emotion, string Asset)[] s_emotions =
        {
            ("fear", "Face_Fear"),
            ("surprise", "Face_Surprise"),
            ("smile", "Face_Smile"),
            ("sleep", "Face_Sleep")
        };

        [MenuItem("Game/Scenarios/Setup Cast: Unattended Item")]
        public static void BuildUnattendedItem()
        {
            Build(UnattendedItemCast);
        }

        public static void Build(string castPath)
        {
            ScenarioRunner runner = Object.FindFirstObjectByType<ScenarioRunner>();
            if (runner == null)
            {
                Debug.LogError("Build the scenario UI first (Game/Scenarios/Build Scenario UI In Scene).");
                return;
            }

            JObject cast = JObject.Parse(File.ReadAllText(castPath));
            Selection.activeGameObject = null;
            GameObject existing = GameObject.Find(CastRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(CastRootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup scenario cast");
            Dictionary<string, Transform> groups = BuildGroups(cast, root.transform);

            GameObject male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePrefabPath);
            GameObject female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePrefabPath);
            TextAsset scenario = AssetDatabase.LoadAssetAtPath<TextAsset>((string)cast["scenario"]);
            string forcedVariant = (string)cast["forcedVariant"] ?? string.Empty;

            foreach (JObject actor in cast["actors"])
            {
                string prefabKind = (string)actor["prefab"];
                GameObject instance;
                if (string.IsNullOrEmpty(prefabKind))
                {
                    instance = new GameObject((string)actor["id"]);
                    instance.transform.SetParent(root.transform, false);
                }
                else
                {
                    Transform parent = groups[(string)actor["group"]];
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabKind == "female" ? female : male, parent);
                    instance.name = (string)actor["id"];
                }

                ConfigureActor(instance, actor, runner, scenario, forcedVariant);
            }

            JArray objects = (JArray)cast["objects"];
            if (objects != null)
            {
                foreach (JObject entry in objects)
                {
                    ConfigureSceneObject(entry, runner);
                }
            }

            JArray prefabs = (JArray)cast["prefabs"];
            if (prefabs != null)
            {
                foreach (JObject entry in prefabs)
                {
                    ConfigurePrefab(entry, groups, runner);
                }
            }

            JArray points = (JArray)cast["points"];
            if (points != null)
            {
                foreach (JObject entry in points)
                {
                    ConfigurePoint(entry, groups, runner);
                }
            }

            JArray lockedProps = (JArray)cast["lockedProps"];
            if (lockedProps != null)
            {
                foreach (JToken propName in lockedProps)
                {
                    LockProp((string)propName);
                }
            }

            BriefingView briefing = Object.FindFirstObjectByType<BriefingView>();
            if (briefing != null)
            {
                SerializedObject briefingObject = new SerializedObject(briefing);
                briefingObject.FindProperty("_scenario").objectReferenceValue = scenario;
                briefingObject.FindProperty("_startHint").stringValue = (string)cast["startHint"] ?? string.Empty;
                briefingObject.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log($"Scenario cast built from {castPath}.");
        }

        private static Dictionary<string, Transform> BuildGroups(JObject cast, Transform root)
        {
            Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
            foreach (JObject groupData in cast["groups"])
            {
                string name = (string)groupData["name"];
                GameObject group = new GameObject(name);
                group.transform.SetParent(root, false);
                GameObject actors = new GameObject("Actors");
                actors.transform.SetParent(group.transform, false);

                ScenarioActorGroup component = group.AddComponent<ScenarioActorGroup>();
                SerializedObject componentObject = new SerializedObject(component);
                componentObject.FindProperty("_visibilitySource").objectReferenceValue = GameObject.Find((string)groupData["visibilitySource"]);
                componentObject.FindProperty("_actorsRoot").objectReferenceValue = actors;
                componentObject.ApplyModifiedPropertiesWithoutUndo();
                groups[name] = actors.transform;
            }

            return groups;
        }

        private static void ConfigureActor(GameObject instance, JObject data, ScenarioRunner runner, TextAsset scenario,
            string forcedVariant)
        {
            Passenger passenger = instance.GetComponent<Passenger>();
            PassengerSpot seat = null;
            string seatName = (string)data["seat"];
            if (!string.IsNullOrEmpty(seatName))
            {
                GameObject seatObject = GameObject.Find(seatName);
                if (seatObject == null)
                {
                    Debug.LogWarning($"Seat '{seatName}' for actor '{instance.name}' was not found.", instance);
                }
                else
                {
                    seat = seatObject.GetComponent<PassengerSpot>();
                    instance.transform.SetPositionAndRotation(seatObject.transform.position, seatObject.transform.rotation);
                }
            }

            ScenarioActor actor = instance.AddComponent<ScenarioActor>();
            SerializedObject actorObject = new SerializedObject(actor);
            actorObject.FindProperty("_runner").objectReferenceValue = runner;
            actorObject.FindProperty("_actorId").stringValue = (string)data["id"];
            actorObject.FindProperty("_displayName").stringValue = (string)data["name"] ?? string.Empty;
            actorObject.FindProperty("_face").objectReferenceValue = instance.GetComponent<FaceController>();
            actorObject.FindProperty("_onlyInVariant").stringValue = (string)data["variant"] ?? string.Empty;
            actorObject.FindProperty("_randomizeAppearance").boolValue = passenger != null && ((bool?)data["randomize"] ?? true);
            actorObject.FindProperty("_passenger").objectReferenceValue = passenger;
            actorObject.FindProperty("_seat").objectReferenceValue = seat;
            string poseName = (string)data["pose"] ?? PassengerPose.Sitting.ToString();
            actorObject.FindProperty("_pose").enumValueIndex = (int)System.Enum.Parse(typeof(PassengerPose), poseName);

            SerializedProperty emotions = actorObject.FindProperty("_emotions");
            emotions.ClearArray();
            if (passenger != null)
            {
                foreach ((string Emotion, string Asset) binding in s_emotions)
                {
                    FaceExpression expression = AssetDatabase.LoadAssetAtPath<FaceExpression>($"{ExpressionsFolder}/{binding.Asset}.asset");
                    if (expression == null)
                    {
                        continue;
                    }

                    emotions.InsertArrayElementAtIndex(emotions.arraySize);
                    SerializedProperty entry = emotions.GetArrayElementAtIndex(emotions.arraySize - 1);
                    entry.FindPropertyRelative("_emotion").stringValue = binding.Emotion;
                    entry.FindPropertyRelative("_expression").objectReferenceValue = expression;
                }
            }

            actorObject.ApplyModifiedPropertiesWithoutUndo();

            string target = (string)data["target"];
            bool isStarter = (bool?)data["starter"] ?? false;
            if (!string.IsNullOrEmpty(target) || isStarter)
            {
                CapsuleCollider collider = instance.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 0.7f, 0f);
                collider.radius = 0.3f;
                collider.height = 1.4f;
            }

            if (!string.IsNullOrEmpty(target))
            {
                AddInteractable(instance, runner, target, ReadVector(data["marker"], MarkerOffset));
            }

            if (isStarter)
            {
                ScenarioStarter starter = instance.AddComponent<ScenarioStarter>();
                SerializedObject starterObject = new SerializedObject(starter);
                starterObject.FindProperty("_runner").objectReferenceValue = runner;
                starterObject.FindProperty("_scenario").objectReferenceValue = scenario;
                starterObject.FindProperty("_forcedVariant").stringValue = forcedVariant;
                starterObject.FindProperty("_marker").objectReferenceValue = CreateMarker(instance.transform, "Marker_Start", StartMarkerColor,
                    MarkerOffset + Vector3.up * 0.25f);
                starterObject.ApplyModifiedPropertiesWithoutUndo();
            }

            JArray props = (JArray)data["props"];
            if (props != null)
            {
                foreach (JToken prop in props)
                {
                    AddProp(instance.transform, (string)prop);
                }
            }
        }

        // An existing scene object (a prop) that becomes a world target.
        private static void ConfigureSceneObject(JObject data, ScenarioRunner runner)
        {
            string name = (string)data["name"];
            GameObject found = GameObject.Find(name);
            if (found == null)
            {
                Debug.LogWarning($"Scene object '{name}' was not found.");
                return;
            }

            VSMTaskProp prop = found.GetComponentInParent<VSMTaskProp>();
            GameObject host = prop != null ? prop.gameObject : found;
            if (host.GetComponentInChildren<Collider>() == null)
            {
                host.AddComponent<BoxCollider>();
            }

            AddInteractable(host, runner, (string)data["target"], ReadVector(data["marker"], ObjectMarkerOffset));
        }

        // A prop instantiated for the scenario, e.g. a ticket terminal in a wagon that has none.
        private static void ConfigurePrefab(JObject data, Dictionary<string, Transform> groups, ScenarioRunner runner)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)data["prefab"]);
            if (prefab == null)
            {
                Debug.LogWarning($"Prefab '{(string)data["prefab"]}' was not found.");
                return;
            }

            Transform parent = groups[(string)data["group"]];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = (string)data["id"];
            instance.transform.position = ReadVector(data["position"], Vector3.zero);
            instance.transform.rotation = Quaternion.Euler(ReadVector(data["rotation"], Vector3.zero));
            VSMTaskProp prop = instance.GetComponent<VSMTaskProp>();
            if (prop != null)
            {
                prop.portable = false;
            }

            if (instance.GetComponentInChildren<Collider>() == null)
            {
                instance.AddComponent<BoxCollider>();
            }

            AddInteractable(instance, runner, (string)data["target"], ReadVector(data["marker"], ObjectMarkerOffset));
        }

        // An invisible interaction volume: a seat to inspect, a wall panel by a door.
        private static void ConfigurePoint(JObject data, Dictionary<string, Transform> groups, ScenarioRunner runner)
        {
            Transform parent = groups[(string)data["group"]];
            GameObject point = new GameObject((string)data["id"]);
            point.transform.SetParent(parent, false);
            string seatName = (string)data["seat"];
            Vector3 position = ReadVector(data["position"], Vector3.zero);
            if (!string.IsNullOrEmpty(seatName))
            {
                GameObject seat = GameObject.Find(seatName);
                if (seat == null)
                {
                    Debug.LogWarning($"Seat '{seatName}' for point '{point.name}' was not found.");
                }
                else
                {
                    position = seat.transform.position + ReadVector(data["offset"], Vector3.zero);
                }
            }

            point.transform.position = position;
            BoxCollider collider = point.AddComponent<BoxCollider>();
            collider.size = ReadVector(data["size"], new Vector3(0.4f, 0.4f, 0.4f));
            AddInteractable(point, runner, (string)data["target"], ReadVector(data["marker"], ObjectMarkerOffset));
        }

        private static void AddInteractable(GameObject host, ScenarioRunner runner, string target, Vector3 markerOffset)
        {
            ScenarioInteractable interactable = host.AddComponent<ScenarioInteractable>();
            SerializedObject interactableObject = new SerializedObject(interactable);
            interactableObject.FindProperty("_runner").objectReferenceValue = runner;
            interactableObject.FindProperty("_targetId").stringValue = target;
            interactableObject.FindProperty("_marker").objectReferenceValue = CreateMarker(host.transform, "Marker_Target", TargetMarkerColor, markerOffset);
            interactableObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 ReadVector(JToken token, Vector3 fallback)
        {
            JArray array = token as JArray;
            if (array == null || array.Count != 3)
            {
                return fallback;
            }

            return new Vector3((float)array[0], (float)array[1], (float)array[2]);
        }

        // A spinning diamond above the object; the owning component shows it only while the object matters.
        private static GameObject CreateMarker(Transform actor, string materialName, Color color, Vector3 offset)
        {
            GameObject marker = new GameObject("Marker");
            marker.transform.SetParent(actor, false);
            marker.transform.localPosition = offset;
            marker.AddComponent<ScenarioMarker>();

            GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "Diamond";
            Object.DestroyImmediate(diamond.GetComponent<Collider>());
            diamond.transform.SetParent(marker.transform, false);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            diamond.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
            diamond.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(materialName, color, UnlitShader);
            return marker;
        }

        // Placeholder props until the distinctive character model is made: the owner must be recognizable by description.
        private static void AddProp(Transform actor, string kind)
        {
            GameObject prop;
            switch (kind)
            {
                case "cap":
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.transform.localScale = new Vector3(0.24f, 0.05f, 0.27f);
                    prop.transform.localPosition = new Vector3(0f, 1.27f, 0.02f);
                    prop.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Prop_Cap", new Color(0.45f, 0.28f, 0.18f));
                    break;
                case "scarf":
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    prop.transform.localScale = new Vector3(0.24f, 0.05f, 0.24f);
                    prop.transform.localPosition = new Vector3(0f, 1.0f, 0.02f);
                    prop.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Prop_Scarf", new Color(1f, 0.5f, 0.05f));
                    break;
                case "thermos":
                    prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    prop.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
                    prop.transform.localPosition = new Vector3(0.2f, 0.85f, 0.45f);
                    prop.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Prop_Thermos", new Color(0.2f, 0.22f, 0.25f));
                    break;
                default:
                    Debug.LogWarning($"Unknown prop '{kind}'.");
                    return;
            }

            prop.name = "Prop_" + kind;
            Object.DestroyImmediate(prop.GetComponent<Collider>());
            prop.transform.SetParent(actor, false);
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            return EnsureMaterial(name, color, LitShader);
        }

        private static Material EnsureMaterial(string name, Color color, string shaderName)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialsFolder).Replace('\\', '/'), Path.GetFileName(MaterialsFolder));
            }

            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        private static void LockProp(string name)
        {
            GameObject propObject = GameObject.Find(name);
            if (propObject == null)
            {
                Debug.LogWarning($"Prop '{name}' was not found.");
                return;
            }

            VSMTaskProp prop = propObject.GetComponentInParent<VSMTaskProp>();
            if (prop != null && prop.portable)
            {
                Undo.RecordObject(prop, "Lock scenario prop");
                prop.portable = false;
                EditorUtility.SetDirty(prop);
            }
        }
    }
}
