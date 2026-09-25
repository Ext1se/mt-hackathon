using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters.Face
{
    /// <summary>
    /// A facial expression as a set of ARKit parameter weights. Parameters that are not listed stay at 0.
    /// </summary>
    [CreateAssetMenu(fileName = "FaceExpression", menuName = "Game/Characters/Face Expression")]
    public class FaceExpression : ScriptableObject
    {
        [SerializeField] private List<FaceParameterWeight> _weights = new List<FaceParameterWeight>();

        [Tooltip("Crossfade time into this expression. Negative uses the FaceController default.")]
        [SerializeField] private float _fadeTime = -1f;

        public IReadOnlyList<FaceParameterWeight> Weights => _weights;
        public float FadeTime => _fadeTime;

        /// <summary>
        /// Writes the expression into a dense array indexed by <see cref="FaceParameter"/>, scaled by intensity.
        /// </summary>
        public void Fill(float[] weights, float intensity)
        {
            Array.Clear(weights, 0, weights.Length);
            for (int i = 0; i < _weights.Count; i++)
            {
                FaceParameterWeight entry = _weights[i];
                weights[(int)entry.Parameter] = Mathf.Clamp01(entry.Weight * intensity);
            }
        }
    }
}
