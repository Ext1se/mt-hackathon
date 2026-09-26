using System.Collections.Generic;
using System.IO;
using Game.Characters.Face;
using Game.Characters.Passengers;
using Game.Scenarios.Presentation;
using Game.Scenarios.Presentation.UI;
using Game.Scenarios.Presentation.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VSM.Interaction;
using VSM.Player;
using VSM.Presentation;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Places a scenario's cast in the open scene from a *.cast.json file: passengers on fixed seats with actor ids,
    /// emotions, world-interaction targets, placeholder props (cap, scarf, thermos) and the scenario starter.
    /// Running it again replaces the previous cast. Requires the scenario UI (ScenarioRunner) in the scene.
    /// The cast lives under the swaying train root, so the actors move with their seats.
    /// Optional sections: "marks" (extra standing or sitting spots) and "states" (scene changes with a fade, see
    /// <see cref="ScenarioWorldStates"/>).
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
            VSMTrainMotion train = Object.FindFirstObjectByType<VSMTrainMotion>();
            if (train != null)
            {
                root.transform.SetParent(train.transform, false);
            }

            Dictionary<string, Transform> groups = BuildGroups(cast, root.transform);
            Dictionary<string, GameObject> actors = new Dictionary<string, GameObject>();

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
                ConfigureHeadLook(instance, (JObject)cast["headLook"], (JObject)actor["headLook"]);
                actors[(string)actor["id"]] = instance;
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

            Dictionary<string, PassengerSpot> marks = new Dictionary<string, PassengerSpot>();
            JArray markEntries = (JArray)cast["marks"];
            if (markEntries != null)
            {
                foreach (JObject entry in markEntries)
                {
                    PassengerSpot mark = CreateMark(entry, groups);
                    marks[mark.name] = mark;
                }
            }

            JArray zones = (JArray)cast["zones"];
            if (zones != null)
            {
                foreach (JObject entry in zones)
                {
                    CreateZone(entry, groups, runner);
                }
            }

            JArray states = (JArray)cast["states"];
            if (states != null)
            {
                BuildWorldStates(root, runner, states, actors, marks);
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
            actorObject.FindProperty("_emotionIntensity").floatValue = (float?)data["emotionIntensity"] ?? 1f;
            actorObject.FindProperty("_holdClip").stringValue = (string)data["holdClip"] ?? string.Empty;
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

            AddStandPoint(instance, data);
        }

        // "headLook" at the top of the cast file sets defaults for every passenger actor; an actor's own "headLook"
        // overrides single fields, e.g. { "enabled": false } or { "maxYaw": 40 }.
        private static void ConfigureHeadLook(GameObject instance, JObject defaults, JObject overrides)
        {
            Passenger passenger = instance.GetComponent<Passenger>();
            if (passenger == null)
            {
                return;
            }

            JObject settings = new JObject();
            if (defaults != null)
            {
                settings.Merge(defaults);
            }

            if (overrides != null)
            {
                settings.Merge(overrides);
            }

            PassengerHeadLook look = instance.AddComponent<PassengerHeadLook>();
            SerializedObject lookObject = new SerializedObject(look);
            lookObject.FindProperty("_lookAtPlayer").boolValue = (bool?)settings["enabled"] ?? false;
            lookObject.FindProperty("_animator").objectReferenceValue = instance.GetComponent<Animator>();
            lookObject.FindProperty("_passenger").objectReferenceValue = passenger;
            Camera view = Camera.main;
            lookObject.FindProperty("_target").objectReferenceValue = view != null ? view.transform : null;
            SetFloat(lookObject, "_maxYaw", settings["maxYaw"]);
            SetFloat(lookObject, "_maxPitchUp", settings["maxPitchUp"]);
            SetFloat(lookObject, "_maxPitchDown", settings["maxPitchDown"]);
            SetFloat(lookObject, "_releaseMargin", settings["releaseMargin"]);
            SetFloat(lookObject, "_maxDistance", settings["maxDistance"]);
            SetFloat(lookObject, "_neckShare", settings["neckShare"]);
            lookObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(SerializedObject target, string property, JToken value)
        {
            if (value != null)
            {
                target.FindProperty(property).floatValue = (float)value;
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
            AddStandPoint(host, data);
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
            AddStandPoint(instance, data);
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
            AddStandPoint(point, data);
        }

        // A spot for scene changes only, e.g. a place in the aisle where a passenger stands up. Reserved, so no
        // random passenger takes it; it sits next to the group's actors, not under them, to stay out of culling.
        private static PassengerSpot CreateMark(JObject data, Dictionary<string, Transform> groups)
        {
            Transform parent = groups[(string)data["group"]].parent;
            GameObject mark = new GameObject((string)data["id"]);
            mark.transform.SetParent(parent, false);
            mark.transform.position = ReadVector(data["position"], Vector3.zero);
            mark.transform.rotation = Quaternion.Euler(0f, (float?)data["yaw"] ?? 0f, 0f);
            PassengerSpot spot = mark.AddComponent<PassengerSpot>();
            SerializedObject spotObject = new SerializedObject(spot);
            string kind = (string)data["kind"] ?? PassengerSpotKind.Standing.ToString();
            spotObject.FindProperty("_kind").enumValueIndex = (int)System.Enum.Parse(typeof(PassengerSpotKind), kind);
            spotObject.FindProperty("_reserved").boolValue = true;
            spotObject.ApplyModifiedPropertiesWithoutUndo();
            return spot;
        }

        // A line across the wagon that signals the scenario when the player crosses it (see ScenarioZone).
        private static void CreateZone(JObject data, Dictionary<string, Transform> groups, ScenarioRunner runner)
        {
            Transform parent = groups[(string)data["group"]].parent;
            GameObject zone = new GameObject((string)data["id"]);
            zone.transform.SetParent(parent, false);
            zone.transform.position = ReadVector(data["position"], Vector3.zero);
            zone.transform.rotation = Quaternion.Euler(0f, (float?)data["yaw"] ?? 0f, 0f);
            ScenarioZone component = zone.AddComponent<ScenarioZone>();
            SerializedObject zoneObject = new SerializedObject(component);
            zoneObject.FindProperty("_runner").objectReferenceValue = runner;
            VSMWalkController player = Object.FindFirstObjectByType<VSMWalkController>();
            zoneObject.FindProperty("_player").objectReferenceValue = player != null ? player.transform : null;
            zoneObject.FindProperty("_signal").stringValue = (string)data["signal"] ?? string.Empty;
            zoneObject.FindProperty("_halfWidth").floatValue = (float?)data["halfWidth"] ?? 1.8f;
            zoneObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildWorldStates(GameObject root, ScenarioRunner runner, JArray states, Dictionary<string, GameObject> actors,
            Dictionary<string, PassengerSpot> marks)
        {
            ScenarioWorldStates world = root.AddComponent<ScenarioWorldStates>();
            SerializedObject worldObject = new SerializedObject(world);
            worldObject.FindProperty("_runner").objectReferenceValue = runner;
            SerializedProperty stateList = worldObject.FindProperty("_states");
            stateList.ClearArray();
            foreach (JObject state in states)
            {
                string stateId = (string)state["id"];
                stateList.InsertArrayElementAtIndex(stateList.arraySize);
                SerializedProperty stateProperty = stateList.GetArrayElementAtIndex(stateList.arraySize - 1);
                stateProperty.FindPropertyRelative("_id").stringValue = stateId;
                JToken when = state["when"];
                stateProperty.FindPropertyRelative("_conditions").stringValue = when != null ? when.ToString(Formatting.None) : "[]";
                stateProperty.FindPropertyRelative("_fade").boolValue = (bool?)state["fade"] ?? true;

                SerializedProperty actionList = stateProperty.FindPropertyRelative("_actions");
                actionList.ClearArray();
                foreach (JObject action in state["actions"])
                {
                    actionList.InsertArrayElementAtIndex(actionList.arraySize);
                    FillWorldAction(actionList.GetArrayElementAtIndex(actionList.arraySize - 1), action, actors, marks, stateId);
                }
            }

            worldObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillWorldAction(SerializedProperty property, JObject data, Dictionary<string, GameObject> actors,
            Dictionary<string, PassengerSpot> marks, string stateId)
        {
            string type = (string)data["type"];
            SerializedProperty objects = property.FindPropertyRelative("_objects");
            objects.ClearArray();
            switch (type)
            {
                case "hide":
                case "show":
                    property.FindPropertyRelative("_kind").enumValueIndex = (int)(type == "hide" ? WorldActionKind.Hide : WorldActionKind.Show);
                    List<GameObject> targets = new List<GameObject>();
                    AddActors((JArray)data["actors"], actors, targets, stateId);
                    string crowd = (string)data["crowd"];
                    if (!string.IsNullOrEmpty(crowd))
                    {
                        AddCrowd(crowd, targets, stateId);
                    }

                    foreach (GameObject target in targets)
                    {
                        objects.InsertArrayElementAtIndex(objects.arraySize);
                        objects.GetArrayElementAtIndex(objects.arraySize - 1).objectReferenceValue = target;
                    }

                    break;
                case "move":
                    property.FindPropertyRelative("_kind").enumValueIndex = (int)WorldActionKind.Move;
                    property.FindPropertyRelative("_passenger").objectReferenceValue = FindMovedPassenger(data, actors, stateId);
                    property.FindPropertyRelative("_spot").objectReferenceValue = FindSpot((string)data["to"], marks, stateId);
                    string poseName = (string)data["pose"] ?? PassengerPose.Sitting.ToString();
                    property.FindPropertyRelative("_pose").enumValueIndex = (int)System.Enum.Parse(typeof(PassengerPose), poseName);
                    // "clip" present replaces the held clip ("" lets the passenger vary); absent keeps the current one.
                    JToken clip = data["clip"];
                    property.FindPropertyRelative("_setsHeldClip").boolValue = clip != null;
                    property.FindPropertyRelative("_heldClip").stringValue = clip != null ? (string)clip : string.Empty;
                    break;
                default:
                    Debug.LogWarning($"World state '{stateId}': unknown action type '{type}'.");
                    break;
            }
        }

        private static void AddActors(JArray ids, Dictionary<string, GameObject> actors, List<GameObject> targets, string stateId)
        {
            if (ids == null)
            {
                return;
            }

            foreach (JToken id in ids)
            {
                if (actors.TryGetValue((string)id, out GameObject actor))
                {
                    targets.Add(actor);
                }
                else
                {
                    Debug.LogWarning($"World state '{stateId}': actor '{(string)id}' was not found.");
                }
            }
        }

        // Every placed passenger of a wagon's spawner, e.g. "Passengers_C03".
        private static void AddCrowd(string spawnerName, List<GameObject> targets, string stateId)
        {
            GameObject spawnerObject = GameObject.Find(spawnerName);
            PassengerSpawner spawner = spawnerObject != null ? spawnerObject.GetComponent<PassengerSpawner>() : null;
            if (spawner == null)
            {
                Debug.LogWarning($"World state '{stateId}': spawner '{spawnerName}' was not found.");
                return;
            }

            foreach (Transform child in spawner.PassengersRoot)
            {
                if (child.GetComponent<Passenger>() != null)
                {
                    targets.Add(child.gameObject);
                }
            }
        }

        // "actor": a scenario actor id; "occupant": the placed passenger sitting on that seat in the editor.
        private static Passenger FindMovedPassenger(JObject data, Dictionary<string, GameObject> actors, string stateId)
        {
            string actorId = (string)data["actor"];
            if (!string.IsNullOrEmpty(actorId))
            {
                Passenger passenger = actors.TryGetValue(actorId, out GameObject actor) ? actor.GetComponent<Passenger>() : null;
                if (passenger == null)
                {
                    Debug.LogWarning($"World state '{stateId}': actor '{actorId}' is not a passenger.");
                }

                return passenger;
            }

            string seat = (string)data["occupant"];
            foreach (PlacedPassenger placed in Object.FindObjectsByType<PlacedPassenger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (placed.Spot != null && placed.Spot.name == seat)
                {
                    return placed.GetComponent<Passenger>();
                }
            }

            Debug.LogWarning($"World state '{stateId}': nobody sits on '{seat}'.");
            return null;
        }

        private static PassengerSpot FindSpot(string name, Dictionary<string, PassengerSpot> marks, string stateId)
        {
            if (marks.TryGetValue(name, out PassengerSpot mark))
            {
                return mark;
            }

            GameObject found = GameObject.Find(name);
            PassengerSpot spot = found != null ? found.GetComponent<PassengerSpot>() : null;
            if (spot == null)
            {
                Debug.LogWarning($"World state '{stateId}': spot '{name}' was not found.");
            }

            return spot;
        }

        // "stand": where the player is put, facing the object, before talking to it or using it. A child of the object,
        // so it follows a passenger moved by a scene change.
        private static void AddStandPoint(GameObject host, JObject data)
        {
            JToken stand = data["stand"];
            if (stand == null)
            {
                return;
            }

            GameObject point = new GameObject("StandPoint");
            point.transform.SetParent(host.transform, false);
            point.transform.position = ReadVector(stand, host.transform.position);
            SetStandPoint(host.GetComponent<ScenarioStarter>(), point.transform);
            SetStandPoint(host.GetComponent<ScenarioInteractable>(), point.transform);
        }

        private static void SetStandPoint(Component component, Transform point)
        {
            if (component == null)
            {
                return;
            }

            SerializedObject componentObject = new SerializedObject(component);
            componentObject.FindProperty("_standPoint").objectReferenceValue = point;
            componentObject.ApplyModifiedPropertiesWithoutUndo();
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
