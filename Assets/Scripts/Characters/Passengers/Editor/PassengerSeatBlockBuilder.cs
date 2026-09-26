using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Upgrades seat spots made by an older generator run: groups the spots of one seat mesh under a
    /// <see cref="PassengerSeatBlock"/> and assigns the clip set fitted to that seat model. Spot names and positions
    /// are kept, so scenario casts that find seats by name keep working. Spots are matched by the generator naming,
    /// <c>Seat_&lt;mesh&gt;_&lt;index&gt;</c>.
    /// </summary>
    public static class PassengerSeatBlockBuilder
    {
        private const string UndoName = "Group Seats Into Blocks";
        private const string SpotPrefix = "Seat_";
        private const string SeatSetFolder = "Assets/Animations/Passengers/Seats";

        // Seat mesh name fragment (case-insensitive) to the fitted clip set. First match wins; "first2" and "standard2"
        // are not fitted yet and borrow the set of the closest model.
        private static readonly (string Fragment, string SetPath)[] s_animationSets =
        {
            ("first", SeatSetFolder + "/First1/PassengerAnimationSet_First1.asset"),
            ("business", SeatSetFolder + "/Business/PassengerAnimationSet_Business.asset"),
            ("meeting", SeatSetFolder + "/Business/PassengerAnimationSet_Business.asset"),
            ("comfort", SeatSetFolder + "/Comfort/PassengerAnimationSet_Comfort.asset"),
            ("standard", SeatSetFolder + "/Standard/PassengerAnimationSet_Standard.asset")
        };

        [MenuItem("Tools/Passengers/Group Selected Spawner Seats Into Blocks")]
        public static void GroupSelected()
        {
            PassengerSpawner spawner = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PassengerSpawner>()
                : null;
            if (spawner == null)
            {
                Debug.LogError("Select a PassengerSpawner (or an object under one) first.");
                return;
            }

            int blocks = GroupIntoBlocks(spawner);
            int sets = AssignAnimationSets(spawner);
            Debug.Log($"'{spawner.name}': created {blocks} seat blocks, assigned {sets} clip sets.", spawner);
        }

        /// <summary>Wraps seats that share a mesh name under a block; returns how many blocks were created.</summary>
        public static int GroupIntoBlocks(PassengerSpawner spawner)
        {
            List<PassengerSpot> spots = new List<PassengerSpot>();
            spawner.GetComponentsInChildren(true, spots);

            List<string> keys = new List<string>();
            Dictionary<string, List<PassengerSpot>> groups = new Dictionary<string, List<PassengerSpot>>();
            for (int i = 0; i < spots.Count; i++)
            {
                PassengerSpot spot = spots[i];
                if (spot.Kind != PassengerSpotKind.Seat || spot.GetComponentInParent<PassengerSeatBlock>() != null)
                {
                    continue;
                }

                string key = spot.transform.parent.GetInstanceID() + "|" + BlockName(spot.name);
                if (!groups.TryGetValue(key, out List<PassengerSpot> group))
                {
                    group = new List<PassengerSpot>();
                    groups.Add(key, group);
                    keys.Add(key);
                }

                group.Add(spot);
            }

            int created = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                List<PassengerSpot> group = groups[keys[i]];
                if (group.Count < 2)
                {
                    continue;
                }

                CreateBlock(group);
                created++;
            }

            return created;
        }

        /// <summary>Assigns the fitted clip set by seat mesh name to seats without one; returns how many were set.</summary>
        public static int AssignAnimationSets(PassengerSpawner spawner)
        {
            List<PassengerSpot> spots = new List<PassengerSpot>();
            spawner.GetComponentsInChildren(true, spots);
            int assigned = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                PassengerSpot spot = spots[i];
                if (spot.Kind != PassengerSpotKind.Seat || spot.AnimationSet != null)
                {
                    continue;
                }

                PassengerAnimationSet set = FindAnimationSet(spot.name);
                if (set == null)
                {
                    Debug.LogWarning($"No fitted clip set for seat '{spot.name}'.", spot);
                    continue;
                }

                SerializedObject serialized = new SerializedObject(spot);
                serialized.FindProperty("_animationSet").objectReferenceValue = set;
                serialized.ApplyModifiedProperties();
                assigned++;
            }

            return assigned;
        }

        private static void CreateBlock(List<PassengerSpot> group)
        {
            Transform first = group[0].transform;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < group.Count; i++)
            {
                center += group[i].transform.position;
            }

            center /= group.Count;

            GameObject block = new GameObject("Block_" + BlockName(group[0].name));
            Undo.RegisterCreatedObjectUndo(block, UndoName);
            block.transform.SetParent(first.parent, false);
            block.transform.SetPositionAndRotation(center, first.rotation);
            block.transform.SetSiblingIndex(first.GetSiblingIndex());
            Undo.AddComponent<PassengerSeatBlock>(block);

            for (int i = 0; i < group.Count; i++)
            {
                Undo.SetTransformParent(group[i].transform, block.transform, UndoName);
            }
        }

        // "Seat_C02_SEAT_business_10_1" -> "C02_SEAT_business_10": the generator prefix and seat index are stripped.
        private static string BlockName(string spotName)
        {
            string name = spotName.StartsWith(SpotPrefix) ? spotName.Substring(SpotPrefix.Length) : spotName;
            int underscore = name.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(name.Substring(underscore + 1), out int _))
            {
                name = name.Substring(0, underscore);
            }

            return name;
        }

        private static PassengerAnimationSet FindAnimationSet(string spotName)
        {
            string lower = spotName.ToLowerInvariant();
            for (int i = 0; i < s_animationSets.Length; i++)
            {
                if (lower.Contains(s_animationSets[i].Fragment))
                {
                    return AssetDatabase.LoadAssetAtPath<PassengerAnimationSet>(s_animationSets[i].SetPath);
                }
            }

            return null;
        }
    }
}
