using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Tools/Passengers/Place Passengers: arranges passengers of one <see cref="PassengerSpawner"/> in the scene.
    /// Every spot gets a prefab and a pose popup; the fill buttons place a random crowd by the spawner rules
    /// (seat blocks, occupancy, sleep chance, reserved spots), which is then corrected seat by seat.
    /// </summary>
    public class PassengerPlacementWindow : EditorWindow
    {
        private const string NoneOption = "-";
        private const float NameWidth = 230f;
        private const float PrefabWidth = 190f;
        private const float PoseWidth = 110f;
        private const float ButtonWidth = 56f;

        private static readonly string[] s_seatPoseNames = { "Sitting", "Asleep" };
        private static readonly PassengerPose[] s_seatPoses = { PassengerPose.Sitting, PassengerPose.SittingAsleep };

        private PassengerSpawner _spawner;
        private int _seed;
        private bool _onlyEmpty;
        private string _filter = string.Empty;
        private Vector2 _scroll;

        private readonly List<PassengerSpawner> _spawners = new List<PassengerSpawner>();
        private readonly List<PassengerSpot> _spots = new List<PassengerSpot>();
        private readonly List<PassengerSeatBlock> _blocks = new List<PassengerSeatBlock>();
        private readonly List<PassengerSpot> _blockSpots = new List<PassengerSpot>();
        private readonly HashSet<PassengerSpot> _spotsInBlocks = new HashSet<PassengerSpot>();
        private readonly Dictionary<PassengerSpot, PlacedPassenger> _placed = new Dictionary<PassengerSpot, PlacedPassenger>();
        private readonly List<PlacedPassenger> _orphans = new List<PlacedPassenger>();
        private readonly List<Passenger> _prefabOptions = new List<Passenger>();
        private string[] _prefabNames = new string[0];

        [MenuItem("Tools/Passengers/Place Passengers")]
        public static void Open()
        {
            GetWindow<PassengerPlacementWindow>("Passengers");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
            PickSpawnerFromSelection();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnSelectionChange()
        {
            PickSpawnerFromSelection();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawSpawnerField();
            if (_spawner == null)
            {
                EditorGUILayout.HelpBox("Select a PassengerSpawner in the scene.", MessageType.Info);
                return;
            }

            Refresh();
            DrawSummary();
            DrawActions();
            DrawSpots();
        }

        private void PickSpawnerFromSelection()
        {
            if (Selection.activeGameObject == null)
            {
                return;
            }

            PassengerSpawner selected = Selection.activeGameObject.GetComponentInParent<PassengerSpawner>();
            if (selected != null)
            {
                _spawner = selected;
            }
        }

        private void Refresh()
        {
            _spots.Clear();
            _spawner.GetComponentsInChildren(true, _spots);
            _blocks.Clear();
            _spawner.GetComponentsInChildren(true, _blocks);
            _spotsInBlocks.Clear();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blockSpots.Clear();
                _blocks[i].GetSpots(_blockSpots);
                for (int j = 0; j < _blockSpots.Count; j++)
                {
                    _spotsInBlocks.Add(_blockSpots[j]);
                }
            }

            _orphans.Clear();
            PassengerPlacer.CollectPlaced(_spawner, _placed, _orphans);

            _prefabOptions.Clear();
            IReadOnlyList<Passenger> prefabs = _spawner.Prefabs;
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null && !_prefabOptions.Contains(prefabs[i]))
                {
                    _prefabOptions.Add(prefabs[i]);
                }
            }

            // Passengers placed from prefabs outside the pool stay selectable, otherwise their popup would show nothing.
            foreach (PlacedPassenger placed in _placed.Values)
            {
                Passenger source = GetSourcePrefab(placed);
                if (source != null && !_prefabOptions.Contains(source))
                {
                    _prefabOptions.Add(source);
                }
            }

            _prefabNames = new string[_prefabOptions.Count + 1];
            _prefabNames[0] = NoneOption;
            for (int i = 0; i < _prefabOptions.Count; i++)
            {
                _prefabNames[i + 1] = _prefabOptions[i].name;
            }
        }

        private void DrawSpawnerField()
        {
            _spawners.Clear();
            _spawners.AddRange(FindObjectsByType<PassengerSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            _spawners.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            string[] names = new string[_spawners.Count];
            int current = -1;
            for (int i = 0; i < _spawners.Count; i++)
            {
                names[i] = _spawners[i].name;
                if (_spawners[i] == _spawner)
                {
                    current = i;
                }
            }

            EditorGUILayout.BeginHorizontal();
            int picked = EditorGUILayout.Popup("Spawner", current, names);
            if (picked >= 0 && picked < _spawners.Count)
            {
                _spawner = _spawners[picked];
            }

            using (new EditorGUI.DisabledScope(_spawner == null))
            {
                if (GUILayout.Button("Select", GUILayout.Width(ButtonWidth)))
                {
                    Selection.activeObject = _spawner;
                    EditorGUIUtility.PingObject(_spawner);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            int seats = 0;
            int standing = 0;
            int reserved = 0;
            for (int i = 0; i < _spots.Count; i++)
            {
                if (_spots[i].Kind == PassengerSpotKind.Seat)
                {
                    seats++;
                }
                else
                {
                    standing++;
                }

                if (_spots[i].IsReserved)
                {
                    reserved++;
                }
            }

            EditorGUILayout.LabelField($"Spots: {seats} seats in {_blocks.Count} blocks, {standing} standing, {reserved} reserved. "
                + $"Placed: {_placed.Count}.");
            EditorGUILayout.LabelField($"Fill uses the spawner values: seats {_spawner.SeatOccupancy:P0}, "
                + $"standing {_spawner.StandingOccupancy:P0}, asleep {_spawner.SleepChance:P0}; {_prefabOptions.Count} prefabs.");

            if (_orphans.Count > 0)
            {
                EditorGUILayout.HelpBox($"{_orphans.Count} placed passengers have no spot or share one; Clear All removes them.",
                    MessageType.Warning);
            }

            if (_spawner.SpawnMode != PassengerSpawnMode.Placed)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("The spawner is in Random On Start mode: it also creates a random crowd in Play Mode.",
                    MessageType.Warning);
                if (GUILayout.Button("Set Placed", GUILayout.Width(90f), GUILayout.Height(38f)))
                {
                    PassengerPlacer.SetSpawnMode(_spawner, PassengerSpawnMode.Placed);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_prefabOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("The spawner has no pool and no prefabs.", MessageType.Error);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            _seed = EditorGUILayout.IntField(new GUIContent("Seed", "0 = different every time"), _seed, GUILayout.Width(220f));
            using (new EditorGUI.DisabledScope(_prefabOptions.Count == 0))
            {
                if (GUILayout.Button("Fill Empty Spots"))
                {
                    RunFill(false);
                }

                if (GUILayout.Button("Refill All"))
                {
                    RunFill(true);
                }
            }

            if (GUILayout.Button("Clear All"))
            {
                int removed = PassengerPlacer.Clear(_spawner);
                Debug.Log($"'{_spawner.name}': removed {removed} placed passengers.", _spawner);
            }

            if (GUILayout.Button("Refresh Poses"))
            {
                PassengerPlacer.RefreshPoses(_spawner);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Filter", _filter);
            _onlyEmpty = GUILayout.Toggle(_onlyEmpty, "Only empty", GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void RunFill(bool replaceExisting)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Fill Passengers");
            if (_spawner.SpawnMode != PassengerSpawnMode.Placed)
            {
                PassengerPlacer.SetSpawnMode(_spawner, PassengerSpawnMode.Placed);
            }

            int placed = PassengerPlacer.FillRandomly(_spawner, _seed, replaceExisting);
            Debug.Log($"'{_spawner.name}': placed {placed} passengers.", _spawner);
        }

        private void DrawSpots()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _blocks.Count; i++)
            {
                _blockSpots.Clear();
                _blocks[i].GetSpots(_blockSpots);
                if (!AnyVisible(_blockSpots))
                {
                    continue;
                }

                EditorGUILayout.LabelField($"{_blocks[i].name} ({_blockSpots.Count} seats)", EditorStyles.boldLabel);
                for (int j = 0; j < _blockSpots.Count; j++)
                {
                    DrawSpot(_blockSpots[j]);
                }
            }

            bool hasSingleSeats = false;
            for (int i = 0; i < _spots.Count; i++)
            {
                PassengerSpot spot = _spots[i];
                if (spot.Kind != PassengerSpotKind.Seat || _spotsInBlocks.Contains(spot) || !IsVisible(spot))
                {
                    continue;
                }

                if (!hasSingleSeats)
                {
                    EditorGUILayout.LabelField("Single seats", EditorStyles.boldLabel);
                    hasSingleSeats = true;
                }

                DrawSpot(spot);
            }

            bool hasStanding = false;
            for (int i = 0; i < _spots.Count; i++)
            {
                PassengerSpot spot = _spots[i];
                if (spot.Kind != PassengerSpotKind.Standing || !IsVisible(spot))
                {
                    continue;
                }

                if (!hasStanding)
                {
                    EditorGUILayout.LabelField("Standing", EditorStyles.boldLabel);
                    hasStanding = true;
                }

                DrawSpot(spot);
            }

            EditorGUILayout.EndScrollView();
        }

        private bool AnyVisible(List<PassengerSpot> spots)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (IsVisible(spots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsVisible(PassengerSpot spot)
        {
            if (_onlyEmpty && (_placed.ContainsKey(spot) || spot.IsReserved))
            {
                return false;
            }

            return string.IsNullOrEmpty(_filter) || spot.name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawSpot(PassengerSpot spot)
        {
            _placed.TryGetValue(spot, out PlacedPassenger placed);

            EditorGUILayout.BeginHorizontal();
            string label = spot.IsReserved ? spot.name + " (reserved)" : spot.name;
            if (GUILayout.Button(label, EditorStyles.label, GUILayout.Width(NameWidth)))
            {
                Selection.activeObject = spot;
                EditorGUIUtility.PingObject(spot);
            }

            Passenger source = placed != null ? GetSourcePrefab(placed) : null;
            int current = source != null ? _prefabOptions.IndexOf(source) + 1 : 0;
            if (placed != null && source == null)
            {
                current = -1;
            }

            using (new EditorGUI.DisabledScope(spot.IsReserved && placed == null))
            {
                int picked = EditorGUILayout.Popup(current, _prefabNames, GUILayout.Width(PrefabWidth));
                if (picked != current && picked >= 0)
                {
                    ChangePrefab(spot, placed, picked);
                    EditorGUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (placed != null)
            {
                if (spot.Kind == PassengerSpotKind.Seat)
                {
                    int poseIndex = placed.Pose == PassengerPose.SittingAsleep ? 1 : 0;
                    int pickedPose = EditorGUILayout.Popup(poseIndex, s_seatPoseNames, GUILayout.Width(PoseWidth));
                    if (pickedPose != poseIndex)
                    {
                        PassengerPlacer.SetPose(placed, s_seatPoses[pickedPose]);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Standing", GUILayout.Width(PoseWidth));
                }

                if (GUILayout.Button("Select", GUILayout.Width(ButtonWidth)))
                {
                    Selection.activeObject = placed.gameObject;
                    EditorGUIUtility.PingObject(placed.gameObject);
                }

                if (GUILayout.Button("Pose", GUILayout.Width(ButtonWidth)))
                {
                    PassengerPosePreview.Apply(placed.Passenger, spot, placed.Pose);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ChangePrefab(PassengerSpot spot, PlacedPassenger placed, int picked)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place Passenger");
            if (_spawner.SpawnMode != PassengerSpawnMode.Placed)
            {
                PassengerPlacer.SetSpawnMode(_spawner, PassengerSpawnMode.Placed);
            }

            if (picked == 0)
            {
                if (placed != null)
                {
                    PassengerPlacer.Remove(placed);
                }

                return;
            }

            PassengerPose pose = spot.Kind == PassengerSpotKind.Seat ? PassengerPose.Sitting : PassengerPose.Standing;
            if (placed != null)
            {
                pose = placed.Pose;
            }

            PassengerPlacer.Place(_spawner, spot, _prefabOptions[picked - 1], pose);
        }

        private static Passenger GetSourcePrefab(PlacedPassenger placed)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(placed.gameObject);
            return source != null ? source.GetComponent<Passenger>() : null;
        }
    }
}
