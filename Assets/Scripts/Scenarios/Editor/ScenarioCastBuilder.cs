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
                collider.center = new Vector3(0f, 0.55f, 0f);
                collider.radius = 0.25f;
                collider.height = 1.1f;
            }

            if (!string.IsNullOrEmpty(target))
            {
                ScenarioInteractable interactable = instance.AddComponent<ScenarioInteractable>();
                SerializedObject interactableObject = new SerializedObject(interactable);
                interactableObject.FindProperty("_runner").objectReferenceValue = runner;
                interactableObject.FindProperty("_targetId").stringValue = target;
                interactableObject.ApplyModifiedPropertiesWithoutUndo();
            }

            if (isStarter)
            {
                ScenarioStarter starter = instance.AddComponent<ScenarioStarter>();
                SerializedObject starterObject = new SerializedObject(starter);
                starterObject.FindProperty("_runner").objectReferenceValue = runner;
                starterObject.FindProperty("_scenario").objectReferenceValue = scenario;
                starterObject.FindProperty("_forcedVariant").stringValue = forcedVariant;
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
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialsFolder).Replace('\\', '/'), Path.GetFileName(MaterialsFolder));
            }

            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                material.color = color;
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
