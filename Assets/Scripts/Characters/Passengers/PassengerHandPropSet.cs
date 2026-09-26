using System;
using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// A hand-held prop (e.g. a phone) and how it sits in the hand for each clip that uses it.
    /// The prefab is first brought into a canonical frame (centre at the pivot, length along +Y, screen along +Z),
    /// then a grip places that pivot in the hand bone of the clip.
    /// </summary>
    [CreateAssetMenu(fileName = "PassengerHandPropSet", menuName = "Game/Characters/Passenger Hand Prop Set")]
    public class PassengerHandPropSet : ScriptableObject
    {
        [Serializable]
        public struct Grip
        {
            [Tooltip("Clip during which the prop is shown.")]
            public AnimationClip Clip;
            public PassengerHand Hand;
            [Tooltip("Pivot position in the hand bone space.")]
            public Vector3 LocalPosition;
            [Tooltip("Pivot rotation in the hand bone space, Euler degrees.")]
            public Vector3 LocalEuler;
        }

        [SerializeField] private GameObject _prefab;
        [Header("Prefab To Canonical Frame")]
        [Tooltip("Prefab root offset under the pivot so the prop centre sits at the pivot origin.")]
        [SerializeField] private Vector3 _prefabLocalPosition;
        [Tooltip("Prefab root rotation under the pivot, Euler degrees.")]
        [SerializeField] private Vector3 _prefabLocalEuler;
        [SerializeField, Min(0.001f)] private float _prefabScale = 1f;
        [SerializeField] private Grip[] _grips = new Grip[0];

        public GameObject Prefab => _prefab;
        public Vector3 PrefabLocalPosition => _prefabLocalPosition;
        public Quaternion PrefabLocalRotation => Quaternion.Euler(_prefabLocalEuler);
        public float PrefabScale => _prefabScale;

        /// <summary>Finds the grip of <paramref name="clip"/>; false when the clip uses no prop.</summary>
        public bool TryGetGrip(AnimationClip clip, out Grip grip)
        {
            for (int i = 0; i < _grips.Length; i++)
            {
                if (_grips[i].Clip == clip)
                {
                    grip = _grips[i];
                    return true;
                }
            }

            grip = default;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Editor only: fills the asset (used by the fitting code).</summary>
        public void Configure(GameObject prefab, Vector3 prefabLocalPosition, Vector3 prefabLocalEuler, float prefabScale, Grip[] grips)
        {
            _prefab = prefab;
            _prefabLocalPosition = prefabLocalPosition;
            _prefabLocalEuler = prefabLocalEuler;
            _prefabScale = prefabScale;
            _grips = grips;
        }
#endif
    }
}
