using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Body clips a passenger picks from for each pose. Every clip must have a base-layer state with the same name
    /// in the passenger Animator Controller (Tools/Passengers/Rebuild Animator Controller creates them).
    /// Non-looping clips are played once as a short action and never chosen as the first clip of a pose.
    /// </summary>
    [CreateAssetMenu(fileName = "PassengerAnimationSet", menuName = "Game/Characters/Passenger Animation Set")]
    public class PassengerAnimationSet : ScriptableObject
    {
        [SerializeField] private AnimationClip[] _standing = new AnimationClip[0];
        [SerializeField] private AnimationClip[] _sitting = new AnimationClip[0];
        [Tooltip("Calm sitting clips; the sleep head layer is applied on top of them.")]
        [SerializeField] private AnimationClip[] _sittingAsleep = new AnimationClip[0];

        [Header("Variation")]
        [Tooltip("Seconds a looping clip plays before the passenger may switch to another clip of the same pose.")]
        [SerializeField] private Vector2 _switchInterval = new Vector2(8f, 20f);
        [SerializeField, Range(0f, 1f)] private float _crossFadeTime = 0.4f;
        [Tooltip("Per-passenger playback speed, so a full wagon does not breathe in unison.")]
        [SerializeField] private Vector2 _speedRange = new Vector2(0.9f, 1.1f);

        [Header("Placement")]
        [Tooltip("Metres a sitting or sleeping passenger is moved forward from the seat spot (away from the backrest); "
            + "negative moves back.")]
        [SerializeField, Range(-0.15f, 0.15f)] private float _seatedForwardOffset = 0.03f;

        public Vector2 SwitchInterval => _switchInterval;
        public float CrossFadeTime => _crossFadeTime;
        public Vector2 SpeedRange => _speedRange;
        public float SeatedForwardOffset => _seatedForwardOffset;

        public AnimationClip[] GetClips(PassengerPose pose)
        {
            switch (pose)
            {
                case PassengerPose.Sitting:
                    return _sitting;
                case PassengerPose.SittingAsleep:
                    return _sittingAsleep;
                default:
                    return _standing;
            }
        }
    }
}
