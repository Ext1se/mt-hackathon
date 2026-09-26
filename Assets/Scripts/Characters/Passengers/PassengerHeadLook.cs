using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Turns a passenger's head (and a little of the neck) toward a target, usually the player's camera, on top of the
    /// animation. The turn is clamped per axis relative to the body: yaw left/right, pitch up and down. Beyond the limits
    /// plus a margin, or too far away, or asleep, the head smoothly returns to the animation.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public sealed class PassengerHeadLook : MonoBehaviour
    {
        [Tooltip("Master switch: the passenger may turn the head toward the target.")]
        [SerializeField] private bool _lookAtPlayer = true;
        [Tooltip("What to look at; the player's camera. Falls back to the main camera.")]
        [SerializeField] private Transform _target;
        [SerializeField] private Animator _animator;
        [Tooltip("Optional: a sleeping passenger does not look.")]
        [SerializeField] private Passenger _passenger;

        [Header("Limits (degrees, relative to the body)")]
        [SerializeField, Range(0f, 90f)] private float _maxYaw = 70f;
        [SerializeField, Range(0f, 60f)] private float _maxPitchUp = 20f;
        [SerializeField, Range(0f, 60f)] private float _maxPitchDown = 30f;
        [Tooltip("Past a limit by more than this, the passenger stops looking instead of straining at the limit.")]
        [SerializeField, Range(0f, 90f)] private float _releaseMargin = 30f;

        [Header("Behaviour")]
        [Tooltip("Metres; farther targets are ignored.")]
        [SerializeField, Range(0.5f, 10f)] private float _maxDistance = 3f;
        [Tooltip("Share of the turn done by the neck; the head does the rest.")]
        [SerializeField, Range(0f, 1f)] private float _neckShare = 0.35f;
        [Tooltip("How fast the gaze follows the target (higher is snappier).")]
        [SerializeField, Range(0.5f, 20f)] private float _followSpeed = 5f;
        [Tooltip("Seconds to blend the look in or out.")]
        [SerializeField, Range(0.05f, 2f)] private float _blendSeconds = 0.5f;

        private Transform _head;
        private Transform _neck;
        private Vector3 _headForwardLocal;
        private float _yaw;
        private float _pitch;
        private float _weight;

        /// <summary>Allows or forbids the look at runtime; the head blends back to the animation when forbidden.</summary>
        public bool LookAtPlayer
        {
            get => _lookAtPlayer;
            set => _lookAtPlayer = value;
        }

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator.isHuman)
            {
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
                _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            }

            // Rigs differ in which head axis points forward (this one uses -Y). The saved pose may tilt the head a little,
            // so the axis closest to the body's forward is taken, not the exact direction.
            if (_head != null)
            {
                _headForwardLocal = NearestAxis(Quaternion.Inverse(_head.rotation) * transform.forward);
            }
        }

        private void Start()
        {
            if (_target == null && Camera.main != null)
            {
                _target = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                return;
            }

            bool wantsLook = TryGetTargetAngles(out float targetYaw, out float targetPitch);
            float blend = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            if (wantsLook)
            {
                // Coming back from the animation the gaze starts where the target is, so it does not sweep across.
                if (_weight <= 0f)
                {
                    _yaw = targetYaw;
                    _pitch = targetPitch;
                }

                _yaw = Mathf.Lerp(_yaw, targetYaw, blend);
                _pitch = Mathf.Lerp(_pitch, targetPitch, blend);
            }

            _weight = Mathf.MoveTowards(_weight, wantsLook ? 1f : 0f, Time.deltaTime / _blendSeconds);
            if (_weight <= 0f)
            {
                return;
            }

            // Pitch is positive upward; rotating about the body's right axis by a negative angle lifts the gaze.
            Vector3 desired = transform.rotation * (Quaternion.Euler(-_pitch, _yaw, 0f) * Vector3.forward);
            float weight = Mathf.SmoothStep(0f, 1f, _weight);
            if (_neck != null && _neckShare > 0f)
            {
                TurnBone(_neck, desired, weight * _neckShare);
            }

            TurnBone(_head, desired, weight);
        }

        private static Vector3 NearestAxis(Vector3 direction)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                return new Vector3(Mathf.Sign(direction.x), 0f, 0f);
            }

            return absolute.y >= absolute.z ? new Vector3(0f, Mathf.Sign(direction.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }

        private bool TryGetTargetAngles(out float yaw, out float pitch)
        {
            yaw = 0f;
            pitch = 0f;
            if (!_lookAtPlayer || _target == null
                || (_passenger != null && _passenger.Pose == PassengerPose.SittingAsleep))
            {
                return false;
            }

            Vector3 toTarget = _target.position - _head.position;
            if (toTarget.sqrMagnitude > _maxDistance * _maxDistance)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformDirection(toTarget);
            float rawYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float rawPitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            if (Mathf.Abs(rawYaw) > _maxYaw + _releaseMargin || rawPitch > _maxPitchUp + _releaseMargin
                || rawPitch < -_maxPitchDown - _releaseMargin)
            {
                return false;
            }

            yaw = Mathf.Clamp(rawYaw, -_maxYaw, _maxYaw);
            pitch = Mathf.Clamp(rawPitch, -_maxPitchDown, _maxPitchUp);
            return true;
        }

        // Rotates the bone so the head's forward axis turns toward the desired direction, on top of the animated pose.
        private void TurnBone(Transform bone, Vector3 desired, float weight)
        {
            Vector3 current = _head.rotation * _headForwardLocal;
            Quaternion turn = Quaternion.FromToRotation(current, desired);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
        }
    }
}
