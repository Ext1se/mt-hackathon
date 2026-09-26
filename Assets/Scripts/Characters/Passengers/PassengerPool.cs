using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Passenger prefabs a spawner or the placement tool picks from, e.g. the business-class pool for premium wagons
    /// and the full pool for the rest of the train.
    /// </summary>
    [CreateAssetMenu(fileName = "PassengerPool", menuName = "Game/Characters/Passenger Pool")]
    public class PassengerPool : ScriptableObject
    {
        [SerializeField] private Passenger[] _prefabs = new Passenger[0];

        public IReadOnlyList<Passenger> Prefabs => _prefabs;

        public Passenger GetRandom()
        {
            return _prefabs.Length == 0 ? null : _prefabs[Random.Range(0, _prefabs.Length)];
        }

#if UNITY_EDITOR
        /// <summary>Editor only: replaces the prefab list (used by the pool baker).</summary>
        public void SetPrefabs(Passenger[] prefabs)
        {
            _prefabs = prefabs;
        }
#endif
    }
}
