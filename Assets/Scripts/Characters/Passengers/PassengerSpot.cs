using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// A place in the wagon a passenger can take. The transform is where the character root goes:
    /// on the floor, facing the direction the passenger looks.
    /// </summary>
    public class PassengerSpot : MonoBehaviour
    {
        /// <summary>
        /// Seat surface height above the spot the sitting clips are authored for. The hips sit right above the root,
        /// so a seat is centred on the spot; a higher seat means lifting the spot by the difference.
        /// </summary>
        public const float SeatHeight = 0.42f;

        private const int CircleSegments = 16;
        private const float StandingRadius = 0.25f;

        private static readonly Vector3 s_seatCenter = new Vector3(0f, SeatHeight, 0f);
        private static readonly Vector3 s_seatSize = new Vector3(0.45f, 0.04f, 0.44f);

        [SerializeField] private PassengerSpotKind _kind = PassengerSpotKind.Seat;
        [Tooltip("Seats only: whether a passenger may fall asleep here.")]
        [SerializeField] private bool _allowSleep = true;
        [Tooltip("Optional: clips fitted to this seat model. Overrides the passenger's own animation set while seated here.")]
        [SerializeField] private PassengerAnimationSet _animationSet;
        [Tooltip("Kept for a scenario actor: the spawner and the placement tool leave this spot empty.")]
        [SerializeField] private bool _reserved;

        private Passenger _occupant;

        public PassengerSpotKind Kind => _kind;
        public PassengerAnimationSet AnimationSet => _animationSet;
        public bool IsReserved => _reserved;
        public Passenger Occupant => _occupant;
        public bool IsFree => _occupant == null;
        /// <summary>Free and not reserved: the spawner may put a random passenger here.</summary>
        public bool IsAvailable => _occupant == null && !_reserved;

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (_occupant != null)
            {
                Gizmos.color = new Color(0.9f, 0.4f, 0.2f);
            }
            else if (_reserved)
            {
                Gizmos.color = new Color(0.4f, 0.6f, 0.9f);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.8f, 0.4f);
            }

            if (_kind == PassengerSpotKind.Seat)
            {
                Gizmos.DrawWireCube(s_seatCenter, s_seatSize);
            }
            else
            {
                DrawCircle(StandingRadius);
            }

            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.4f);
            Gizmos.DrawLine(Vector3.forward * 0.4f, new Vector3(0.08f, 0f, 0.3f));
            Gizmos.DrawLine(Vector3.forward * 0.4f, new Vector3(-0.08f, 0f, 0.3f));
        }

        public bool Allows(PassengerPose pose)
        {
            switch (pose)
            {
                case PassengerPose.Standing:
                    return _kind == PassengerSpotKind.Standing;
                case PassengerPose.SittingAsleep:
                    return _kind == PassengerSpotKind.Seat && _allowSleep;
                default:
                    return _kind == PassengerSpotKind.Seat;
            }
        }

        public void SetOccupant(Passenger passenger)
        {
            _occupant = passenger;
        }

        public void Release(Passenger passenger)
        {
            if (_occupant == passenger)
            {
                _occupant = null;
            }
        }

        private static void DrawCircle(float radius)
        {
            Vector3 previous = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector3 next = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
