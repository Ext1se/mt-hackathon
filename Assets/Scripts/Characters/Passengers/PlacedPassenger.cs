using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// A passenger placed in the scene in the editor: takes its <see cref="PassengerSpot"/> on start instead of being
    /// spawned by a <see cref="PassengerSpawner"/>. Runs before the spawner so the spot is occupied when a spawner in
    /// random mode fills the rest of the wagon.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Passenger))]
    public class PlacedPassenger : MonoBehaviour
    {
        [SerializeField] private Passenger _passenger;
        [SerializeField] private PassengerSpot _spot;
        [SerializeField] private PassengerPose _pose = PassengerPose.Sitting;

        public Passenger Passenger => _passenger;
        public PassengerSpot Spot => _spot;
        public PassengerPose Pose => _pose;

        private void Start()
        {
            if (_passenger == null)
            {
                _passenger = GetComponent<Passenger>();
            }

            if (_spot == null)
            {
                Debug.LogWarning($"{nameof(PlacedPassenger)} '{name}' has no spot.", this);
                return;
            }

            _passenger.TakeSpot(_spot, _pose);
        }

        private void Reset()
        {
            _passenger = GetComponent<Passenger>();
        }

        private void OnDrawGizmosSelected()
        {
            if (_spot != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up, _spot.transform.position);
            }
        }

        /// <summary>Sets the spot and pose; the editor placement tool calls this and then previews the pose.</summary>
        public void Configure(PassengerSpot spot, PassengerPose pose)
        {
            _passenger = GetComponent<Passenger>();
            _spot = spot;
            _pose = pose;
        }
    }
}
