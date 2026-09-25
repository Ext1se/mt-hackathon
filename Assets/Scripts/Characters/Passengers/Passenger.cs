using Game.Characters.Face;
using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Animates a CharacterCustomizer character as a wagon passenger: holds a pose on a <see cref="PassengerSpot"/>,
    /// switches between clips of that pose from time to time and drops the head when asleep.
    /// Requires the passenger Animator Controller (body states on the base layer plus the sleep head layer).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class Passenger : MonoBehaviour
    {
        private const string SleepLayerName = "Sleep Head";
        private const int BodyLayer = 0;

        [SerializeField] private Animator _animator;
        [SerializeField] private PassengerAnimationSet _animationSet;

        [Header("Sleep")]
        [Tooltip("Optional: closes the eyes while asleep.")]
        [SerializeField] private FaceController _face;
        [SerializeField] private FaceExpression _sleepExpression;
        [SerializeField, Min(0.01f)] private float _sleepBlendTime = 1.5f;

        private PassengerSpot _spot;
        private PassengerPose _pose;
        private bool _hasPose;
        private AnimationClip _currentClip;
        private float _nextSwitchTime;
        private int _sleepLayer = -1;
        private float _sleepWeight;

        public PassengerPose Pose => _pose;
        public PassengerSpot Spot => _spot;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            // Poses are authored with root motion baked in; the spot alone decides where the passenger is.
            _animator.applyRootMotion = false;
            _sleepLayer = _animator.GetLayerIndex(SleepLayerName);

            if (_animationSet != null)
            {
                Vector2 speedRange = _animationSet.SpeedRange;
                _animator.speed = Random.Range(speedRange.x, speedRange.y);
            }
        }

        private void OnEnable()
        {
            // An Animator restarts from its default state when re-enabled (e.g. a culled wagon comes back), so restore the pose.
            if (!_hasPose || _currentClip == null)
            {
                return;
            }

            _animator.Play(Animator.StringToHash(_currentClip.name), BodyLayer, Random.value);
            if (_sleepLayer >= 0)
            {
                _animator.SetLayerWeight(_sleepLayer, _sleepWeight);
            }
        }

        private void Update()
        {
            if (!_hasPose)
            {
                return;
            }

            if (_sleepLayer >= 0)
            {
                float target = _pose == PassengerPose.SittingAsleep ? 1f : 0f;
                _sleepWeight = Mathf.MoveTowards(_sleepWeight, target, Time.deltaTime / _sleepBlendTime);
                _animator.SetLayerWeight(_sleepLayer, _sleepWeight);
            }

            if (Time.time >= _nextSwitchTime)
            {
                AnimationClip next = PickClip(_currentClip == null || !_currentClip.isLooping);
                PlayClip(next, _animationSet.CrossFadeTime, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_spot != null)
            {
                _spot.Release(this);
            }
        }

        private void Reset()
        {
            _animator = GetComponent<Animator>();
            _face = GetComponent<FaceController>();
        }

        /// <summary>Moves the passenger onto <paramref name="spot"/> and starts <paramref name="pose"/> there.</summary>
        public void TakeSpot(PassengerSpot spot, PassengerPose pose)
        {
            if (_spot != null)
            {
                _spot.Release(this);
            }

            _spot = spot;
            _spot.SetOccupant(this);
            transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
            SetPose(pose);
        }

        public void SetPose(PassengerPose pose)
        {
            if (_animationSet == null)
            {
                Debug.LogWarning($"{nameof(Passenger)} on '{name}' has no animation set.", this);
                return;
            }

            bool isFirstPose = !_hasPose;
            bool wasAsleep = _hasPose && _pose == PassengerPose.SittingAsleep;
            _pose = pose;
            _hasPose = true;

            // The first pose starts at a random point of the clip so neighbours are not in sync.
            float fadeTime = isFirstPose ? 0f : _animationSet.CrossFadeTime;
            PlayClip(PickClip(true), fadeTime, isFirstPose ? Random.value : 0f);

            bool isAsleep = pose == PassengerPose.SittingAsleep;
            if (isFirstPose && _sleepLayer >= 0)
            {
                _sleepWeight = isAsleep ? 1f : 0f;
                _animator.SetLayerWeight(_sleepLayer, _sleepWeight);
            }

            if (_face != null && _face.isActiveAndEnabled)
            {
                if (isAsleep)
                {
                    _face.SetExpression(_sleepExpression, 1f, isFirstPose ? 0f : _sleepBlendTime);
                }
                else if (wasAsleep)
                {
                    _face.ClearExpression(_sleepBlendTime);
                }
            }
        }

        private AnimationClip PickClip(bool loopingOnly)
        {
            AnimationClip[] clips = _animationSet.GetClips(_pose);

            int candidates = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (IsCandidate(clips[i], loopingOnly))
                {
                    candidates++;
                }
            }

            if (candidates == 0)
            {
                // Only the current clip fits: keep playing it rather than stopping.
                return IsCandidate(_currentClip, loopingOnly, true) ? _currentClip : null;
            }

            int pick = Random.Range(0, candidates);
            for (int i = 0; i < clips.Length; i++)
            {
                if (IsCandidate(clips[i], loopingOnly) && pick-- == 0)
                {
                    return clips[i];
                }
            }

            return null;
        }

        private bool IsCandidate(AnimationClip clip, bool loopingOnly, bool allowCurrent = false)
        {
            if (clip == null || (!allowCurrent && clip == _currentClip))
            {
                return false;
            }

            return !loopingOnly || clip.isLooping;
        }

        private void PlayClip(AnimationClip clip, float fadeTime, float normalizedOffset)
        {
            if (clip == null)
            {
                _nextSwitchTime = float.PositiveInfinity;
                return;
            }

            int stateHash = Animator.StringToHash(clip.name);
            if (!_animator.HasState(BodyLayer, stateHash))
            {
                Debug.LogWarning($"{nameof(Passenger)} on '{name}': Animator has no state '{clip.name}'.", this);
                _nextSwitchTime = float.PositiveInfinity;
                return;
            }

            if (clip != _currentClip)
            {
                _animator.CrossFadeInFixedTime(stateHash, fadeTime, BodyLayer, normalizedOffset * clip.length);
            }

            _currentClip = clip;
            float speed = Mathf.Max(_animator.speed, 0.01f);
            if (clip.isLooping)
            {
                Vector2 interval = _animationSet.SwitchInterval;
                _nextSwitchTime = Time.time + Random.Range(interval.x, interval.y);
            }
            else
            {
                _nextSwitchTime = Time.time + Mathf.Max(0.1f, clip.length / speed - _animationSet.CrossFadeTime);
            }
        }
    }
}
