using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Owns the <see cref="PassengerSpot"/>s under this object. In <see cref="PassengerSpawnMode.Placed"/> mode the
    /// passengers are put in the scene in the editor (Tools/Passengers/Place Passengers) and the spawner only keeps
    /// them in sync with the wagon visibility. In <see cref="PassengerSpawnMode.RandomOnStart"/> mode it fills the spots
    /// with random passengers on start. Spots inside a <see cref="PassengerSeatBlock"/> are filled by the block's
    /// pattern: the block is taken with the seat occupancy chance, then the block decides which of its seats get a
    /// passenger. Other spots are filled one by one. Reserved and occupied spots are skipped.
    /// </summary>
    public class PassengerSpawner : MonoBehaviour
    {
        // CharacterCustomizer lives in Assembly-CSharp, which an asmdef cannot reference, so the randomizer is called by name.
        // Body and outfit are two calls: randomizeCharacterAndOutfit dresses the character a frame later in a coroutine,
        // which never finishes if the wagon is hidden in the meantime. Both of these complete inside the call.
        private const string RandomizeBodyMessage = "randomizeAll";
        private const string RandomizeOutfitMessage = "setRandomOutfit";

        // The code default stays RandomOnStart so spawners saved before this field existed keep spawning;
        // the train scenes set Placed explicitly.
        [Tooltip("Placed: passengers are arranged in the editor and kept in the scene. Random On Start: a new random crowd every run.")]
        [SerializeField] private PassengerSpawnMode _spawnMode = PassengerSpawnMode.RandomOnStart;
        [Tooltip("Prefab pool to pick from; when set, Prefabs below is ignored.")]
        [SerializeField] private PassengerPool _pool;
        [Tooltip("Passenger prefabs (CharacterCustomizer characters with a Passenger component), used when no pool is set.")]
        [SerializeField] private Passenger[] _prefabs = new Passenger[0];
        [SerializeField] private Transform _passengersRoot;

        [Header("Occupancy")]
        [SerializeField, Range(0f, 1f)] private float _seatOccupancy = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _standingOccupancy = 0.4f;
        [Tooltip("Chance that a seated passenger is asleep.")]
        [SerializeField, Range(0f, 1f)] private float _sleepChance = 0.25f;

        [Header("Appearance")]
        [Tooltip("Randomize body and outfit of every passenger through CharacterCustomizer.")]
        [SerializeField] private bool _randomizeAppearance = true;
        [Tooltip("Fixed seed for a repeatable wagon; 0 means a new crowd every run.")]
        [SerializeField] private int _seed;

        [Header("Visibility")]
        [Tooltip("Optional: passengers are shown only while this object is active, e.g. a wagon root culled by a portal system. "
            + "Passengers Root must not be this object or its parent.")]
        [SerializeField] private GameObject _visibilitySource;

        private readonly List<PassengerSpot> _spots = new List<PassengerSpot>();
        private readonly List<PassengerSpot> _chosenSpots = new List<PassengerSpot>();
        private readonly List<PassengerSeatBlock> _blocks = new List<PassengerSeatBlock>();
        private readonly List<Passenger> _passengers = new List<Passenger>();
        private bool _isAppearanceReady;

        public IReadOnlyList<Passenger> Passengers => _passengers;
        public PassengerSpawnMode SpawnMode => _spawnMode;
        public PassengerPool Pool => _pool;
        /// <summary>Prefabs to pick from: the pool when set, otherwise the spawner's own list.</summary>
        public IReadOnlyList<Passenger> Prefabs => _pool != null ? _pool.Prefabs : _prefabs;
        public Transform PassengersRoot => _passengersRoot != null ? _passengersRoot : transform;
        public float SeatOccupancy => _seatOccupancy;
        public float StandingOccupancy => _standingOccupancy;
        public float SleepChance => _sleepChance;

        private void Start()
        {
            if (_seed != 0)
            {
                Random.InitState(_seed);
            }

            if (_visibilitySource != null && (_passengersRoot == null || transform.IsChildOf(_passengersRoot)))
            {
                Debug.LogWarning($"{nameof(PassengerSpawner)} on '{name}': visibility sync needs a separate Passengers Root.", this);
                _visibilitySource = null;
            }

            if (_spawnMode == PassengerSpawnMode.Placed)
            {
                // Placed passengers take their spots themselves; their look comes from the prefab preset.
                _isAppearanceReady = true;
                return;
            }

            Spawn();
        }

        private void LateUpdate()
        {
            // Wait until the characters are built: CharacterCustomizer cannot initialize or randomize inactive objects.
            if (_visibilitySource == null || !_isAppearanceReady)
            {
                return;
            }

            bool isVisible = _visibilitySource.activeInHierarchy;
            if (_passengersRoot.gameObject.activeSelf != isVisible)
            {
                _passengersRoot.gameObject.SetActive(isVisible);
            }
        }

        [ContextMenu("Respawn")]
        public void Spawn()
        {
            Clear();

            IReadOnlyList<Passenger> prefabs = Prefabs;
            if (prefabs.Count == 0)
            {
                Debug.LogWarning($"{nameof(PassengerSpawner)} on '{name}' has no passenger prefabs.", this);
                return;
            }

            ChooseSpots();

            Transform parent = _passengersRoot != null ? _passengersRoot : transform;
            // New characters must stay active until built; LateUpdate hides them again if the wagon is culled.
            parent.gameObject.SetActive(true);
            _isAppearanceReady = !_randomizeAppearance;
            for (int i = 0; i < _chosenSpots.Count; i++)
            {
                PassengerSpot spot = _chosenSpots[i];
                if (!spot.IsAvailable)
                {
                    continue;
                }

                bool isSeat = spot.Kind == PassengerSpotKind.Seat;
                PassengerPose pose = PassengerPose.Standing;
                if (isSeat)
                {
                    pose = spot.Allows(PassengerPose.SittingAsleep) && Random.value < _sleepChance
                        ? PassengerPose.SittingAsleep
                        : PassengerPose.Sitting;
                }

                Passenger prefab = prefabs[Random.Range(0, prefabs.Count)];
                Passenger passenger = Instantiate(prefab, spot.transform.position, spot.transform.rotation, parent);
                passenger.name = $"{prefab.name}_{_passengers.Count:00}";
                passenger.TakeSpot(spot, pose);
                _passengers.Add(passenger);
            }

            if (_randomizeAppearance)
            {
                StartCoroutine(RandomizeAppearance());
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _passengers.Count; i++)
            {
                Passenger passenger = _passengers[i];
                if (passenger == null)
                {
                    continue;
                }

                // Destroy is deferred to the end of the frame; free the spot now so a respawn can reuse it.
                if (passenger.Spot != null)
                {
                    passenger.Spot.Release(passenger);
                }

                Destroy(passenger.gameObject);
            }

            _passengers.Clear();
        }

        private IEnumerator RandomizeAppearance()
        {
            // CharacterCustomization initializes itself in Start, the randomizer needs it to be done.
            yield return null;

            for (int i = 0; i < _passengers.Count; i++)
            {
                if (_passengers[i] != null)
                {
                    GameObject character = _passengers[i].gameObject;
                    character.SendMessage(RandomizeBodyMessage, SendMessageOptions.DontRequireReceiver);
                    character.SendMessage(RandomizeOutfitMessage, SendMessageOptions.DontRequireReceiver);
                }
            }

            // With LoadAsync off both calls finish synchronously, so the crowd is complete now.
            _isAppearanceReady = true;
        }

        private void ChooseSpots()
        {
            _chosenSpots.Clear();

            GetComponentsInChildren(_blocks);
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (Random.value < _seatOccupancy)
                {
                    _blocks[i].ChooseSpots(_chosenSpots);
                }
            }

            GetComponentsInChildren(_spots);
            for (int i = 0; i < _spots.Count; i++)
            {
                PassengerSpot spot = _spots[i];
                if (!spot.IsAvailable || spot.GetComponentInParent<PassengerSeatBlock>() != null)
                {
                    continue;
                }

                float occupancy = spot.Kind == PassengerSpotKind.Seat ? _seatOccupancy : _standingOccupancy;
                if (Random.value < occupancy)
                {
                    _chosenSpots.Add(spot);
                }
            }

            // Random order spreads prefabs and poses evenly instead of by hierarchy.
            Shuffle(_chosenSpots);
        }

        private static void Shuffle(List<PassengerSpot> spots)
        {
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                PassengerSpot temp = spots[i];
                spots[i] = spots[j];
                spots[j] = temp;
            }
        }
    }
}
