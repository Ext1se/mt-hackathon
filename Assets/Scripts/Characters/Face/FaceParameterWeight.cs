using System;
using UnityEngine;

namespace Game.Characters.Face
{
    [Serializable]
    public struct FaceParameterWeight
    {
        [SerializeField] private FaceParameter _parameter;
        [SerializeField, Range(0f, 1f)] private float _weight;

        public FaceParameterWeight(FaceParameter parameter, float weight)
        {
            _parameter = parameter;
            _weight = weight;
        }

        public FaceParameter Parameter => _parameter;
        public float Weight => _weight;
    }
}
