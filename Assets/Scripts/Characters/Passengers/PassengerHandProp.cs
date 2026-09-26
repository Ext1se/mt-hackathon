using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// Shows a hand-held prop while the passenger plays a clip that uses it (a phone for texting and calls) and hides
    /// it for other clips. The prop is created once, under a pivot in the hand bone of the current grip.
    /// </summary>
    [RequireComponent(typeof(Passenger))]
    [RequireComponent(typeof(Animator))]
    public class PassengerHandProp : MonoBehaviour
    {
        private const string PivotName = "HandPropPivot";

        [SerializeField] private Passenger _passenger;
        [SerializeField] private Animator _animator;
        [SerializeField] private PassengerHandPropSet _set;
        [Tooltip("Pivot created for the prop; kept so an editor preview and Play Mode reuse the same instance.")]
        [SerializeField] private Transform _pivot;

        public PassengerHandPropSet Set => _set;

        private void Awake()
        {
            if (_passenger == null)
            {
                _passenger = GetComponent<Passenger>();
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            _passenger.ClipChanged += Apply;
            Apply(_passenger.CurrentClip);
        }

        private void OnDisable()
        {
            _passenger.ClipChanged -= Apply;
        }

        private void Reset()
        {
            _passenger = GetComponent<Passenger>();
            _animator = GetComponent<Animator>();
        }

        /// <summary>Shows the prop in the grip of <paramref name="clip"/> or hides it when the clip has none.</summary>
        public void Apply(AnimationClip clip)
        {
            if (_set == null || _set.Prefab == null)
            {
                return;
            }

            if (clip == null || !_set.TryGetGrip(clip, out PassengerHandPropSet.Grip grip))
            {
                if (_pivot != null)
                {
                    _pivot.gameObject.SetActive(false);
                }

                return;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            HumanBodyBones bone = grip.Hand == PassengerHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            Transform hand = _animator.GetBoneTransform(bone);
            if (hand == null)
            {
                return;
            }

            if (_pivot == null)
            {
                CreatePivot();
            }

            _pivot.SetParent(hand, false);
            _pivot.localPosition = grip.LocalPosition;
            _pivot.localRotation = Quaternion.Euler(grip.LocalEuler);
            _pivot.gameObject.SetActive(true);
        }

        private void CreatePivot()
        {
            GameObject pivot = new GameObject(PivotName);
            _pivot = pivot.transform;

            GameObject prop;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                prop = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(_set.Prefab, gameObject.scene);
                UnityEditor.Undo.RegisterCreatedObjectUndo(pivot, "Create Hand Prop");
                UnityEditor.Undo.RegisterCreatedObjectUndo(prop, "Create Hand Prop");
            }
            else
#endif
            {
                prop = Instantiate(_set.Prefab);
            }

            prop.name = _set.Prefab.name;
            prop.transform.SetParent(_pivot, false);
            prop.transform.localPosition = _set.PrefabLocalPosition;
            prop.transform.localRotation = _set.PrefabLocalRotation;
            prop.transform.localScale = Vector3.one * _set.PrefabScale;
        }
    }
}
