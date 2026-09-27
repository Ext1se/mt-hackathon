using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// Keeps a placeholder prop (cap, scarf) on a body bone of the actor it belongs to, so it sits right in any pose:
    /// placed at the bone plus an offset in the actor's own axes and turned with the actor, not with the bone.
    /// </summary>
    // After the head turn (PassengerHeadLook), so the prop follows the final head position.
    [DefaultExecutionOrder(200)]
    public sealed class ScenarioBoneProp : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private HumanBodyBones _bone = HumanBodyBones.Head;
        [Tooltip("Offset from the bone in the actor's axes (X right, Y up, Z forward), in metres.")]
        [SerializeField] private Vector3 _offset;

        private Transform _boneTransform;

        private void Awake()
        {
            if (_animator != null && _animator.isHuman)
            {
                _boneTransform = _animator.GetBoneTransform(_bone);
            }
        }

        private void LateUpdate()
        {
            if (_boneTransform == null)
            {
                return;
            }

            Transform actor = _animator.transform;
            transform.SetPositionAndRotation(_boneTransform.position + actor.rotation * _offset, actor.rotation);
        }
    }
}
