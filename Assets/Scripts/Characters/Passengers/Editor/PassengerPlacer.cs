using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Places passengers in the scene for a <see cref="PassengerSpawner"/> in <see cref="PassengerSpawnMode.Placed"/>
    /// mode: one prefab instance per spot with a <see cref="PlacedPassenger"/>, previewed in its pose.
    /// The random fill follows the same rules as the runtime spawner: seat blocks are filled by pattern,
    /// reserved and taken spots are skipped, occupancy and sleep chances come from the spawner.
    /// </summary>
    public static class PassengerPlacer
    {
        private const string UndoName = "Place Passengers";

        /// <summary>Collects placed passengers of the spawner keyed by spot; passengers without a spot go to <paramref name="orphans"/>.</summary>
        public static void CollectPlaced(PassengerSpawner spawner, Dictionary<PassengerSpot, PlacedPassenger> result,
            List<PlacedPassenger> orphans = null)
        {
            result.Clear();
            List<PlacedPassenger> placed = new List<PlacedPassenger>();
            spawner.PassengersRoot.GetComponentsInChildren(true, placed);
            if (spawner.PassengersRoot != spawner.transform && !spawner.PassengersRoot.IsChildOf(spawner.transform))
            {
                List<PlacedPassenger> underSpawner = new List<PlacedPassenger>();
                spawner.GetComponentsInChildren(true, underSpawner);
                placed.AddRange(underSpawner);
            }

            for (int i = 0; i < placed.Count; i++)
            {
                PlacedPassenger passenger = placed[i];
                if (passenger.Spot != null && !result.ContainsKey(passenger.Spot))
                {
                    result.Add(passenger.Spot, passenger);
                }
                else if (orphans != null)
                {
                    orphans.Add(passenger);
                }
            }
        }

        /// <summary>Puts an instance of <paramref name="prefab"/> on <paramref name="spot"/>, replacing a passenger placed there before.</summary>
        public static PlacedPassenger Place(PassengerSpawner spawner, PassengerSpot spot, Passenger prefab, PassengerPose pose)
        {
            Dictionary<PassengerSpot, PlacedPassenger> placed = new Dictionary<PassengerSpot, PlacedPassenger>();
            CollectPlaced(spawner, placed);
            if (placed.TryGetValue(spot, out PlacedPassenger existing))
            {
                Remove(existing);
            }

            Transform parent = spawner.PassengersRoot;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, UndoName);
            instance.transform.SetParent(parent, false);
            instance.name = $"{prefab.name}_{spot.name}";

            PlacedPassenger component = Undo.AddComponent<PlacedPassenger>(instance);
            component.Configure(spot, pose);
            EditorUtility.SetDirty(component);
            PassengerPosePreview.Apply(component.Passenger, spot, pose);
            return component;
        }

        public static void SetPose(PlacedPassenger placed, PassengerPose pose)
        {
            Undo.RecordObject(placed, UndoName);
            placed.Configure(placed.Spot, pose);
            EditorUtility.SetDirty(placed);
            PassengerPosePreview.Apply(placed.Passenger, placed.Spot, pose);
        }

        public static void Remove(PlacedPassenger placed)
        {
            Undo.DestroyObjectImmediate(placed.gameObject);
        }

        /// <summary>Removes every placed passenger of the spawner and returns how many were removed.</summary>
        public static int Clear(PassengerSpawner spawner)
        {
            Dictionary<PassengerSpot, PlacedPassenger> placed = new Dictionary<PassengerSpot, PlacedPassenger>();
            List<PlacedPassenger> orphans = new List<PlacedPassenger>();
            CollectPlaced(spawner, placed, orphans);
            foreach (PlacedPassenger passenger in placed.Values)
            {
                Remove(passenger);
            }

            for (int i = 0; i < orphans.Count; i++)
            {
                Remove(orphans[i]);
            }

            return placed.Count + orphans.Count;
        }

        /// <summary>Samples the poses again, e.g. after the spots or the clip sets changed.</summary>
        public static int RefreshPoses(PassengerSpawner spawner)
        {
            Dictionary<PassengerSpot, PlacedPassenger> placed = new Dictionary<PassengerSpot, PlacedPassenger>();
            CollectPlaced(spawner, placed);
            int refreshed = 0;
            foreach (KeyValuePair<PassengerSpot, PlacedPassenger> entry in placed)
            {
                if (PassengerPosePreview.Apply(entry.Value.Passenger, entry.Key, entry.Value.Pose))
                {
                    refreshed++;
                }
            }

            return refreshed;
        }

        /// <summary>
        /// Fills free spots at random by the rules of the spawner. Seed 0 gives a new arrangement every time.
        /// With <paramref name="replaceExisting"/> the placed passengers are removed first.
        /// </summary>
        public static int FillRandomly(PassengerSpawner spawner, int seed, bool replaceExisting)
        {
            IReadOnlyList<Passenger> prefabs = spawner.Prefabs;
            if (prefabs.Count == 0)
            {
                Debug.LogWarning($"'{spawner.name}' has no passenger prefabs or pool.", spawner);
                return 0;
            }

            if (replaceExisting)
            {
                Clear(spawner);
            }

            if (seed != 0)
            {
                Random.InitState(seed);
            }

            Dictionary<PassengerSpot, PlacedPassenger> placed = new Dictionary<PassengerSpot, PlacedPassenger>();
            CollectPlaced(spawner, placed);
            List<PassengerSpot> chosen = new List<PassengerSpot>();

            List<PassengerSeatBlock> blocks = new List<PassengerSeatBlock>();
            spawner.GetComponentsInChildren(true, blocks);
            List<PassengerSpot> blockSpots = new List<PassengerSpot>();
            for (int i = 0; i < blocks.Count; i++)
            {
                blockSpots.Clear();
                blocks[i].GetSpots(blockSpots);
                if (IsAnyTaken(blockSpots, placed))
                {
                    continue;
                }

                if (Random.value < spawner.SeatOccupancy)
                {
                    blocks[i].ChoosePattern(chosen);
                }
            }

            List<PassengerSpot> spots = new List<PassengerSpot>();
            spawner.GetComponentsInChildren(true, spots);
            for (int i = 0; i < spots.Count; i++)
            {
                PassengerSpot spot = spots[i];
                if (IsTaken(spot, placed) || spot.GetComponentInParent<PassengerSeatBlock>() != null)
                {
                    continue;
                }

                float occupancy = spot.Kind == PassengerSpotKind.Seat ? spawner.SeatOccupancy : spawner.StandingOccupancy;
                if (Random.value < occupancy)
                {
                    chosen.Add(spot);
                }
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                PassengerSpot spot = chosen[i];
                PassengerPose pose = PassengerPose.Standing;
                if (spot.Kind == PassengerSpotKind.Seat)
                {
                    pose = spot.Allows(PassengerPose.SittingAsleep) && Random.value < spawner.SleepChance
                        ? PassengerPose.SittingAsleep
                        : PassengerPose.Sitting;
                }

                Place(spawner, spot, prefabs[Random.Range(0, prefabs.Count)], pose);
            }

            return chosen.Count;
        }

        public static void SetSpawnMode(PassengerSpawner spawner, PassengerSpawnMode mode)
        {
            SerializedObject serialized = new SerializedObject(spawner);
            serialized.FindProperty("_spawnMode").intValue = (int)mode;
            serialized.ApplyModifiedProperties();
        }

        private static bool IsTaken(PassengerSpot spot, Dictionary<PassengerSpot, PlacedPassenger> placed)
        {
            return spot.IsReserved || placed.ContainsKey(spot);
        }

        private static bool IsAnyTaken(List<PassengerSpot> spots, Dictionary<PassengerSpot, PlacedPassenger> placed)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (IsTaken(spots[i], placed))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
