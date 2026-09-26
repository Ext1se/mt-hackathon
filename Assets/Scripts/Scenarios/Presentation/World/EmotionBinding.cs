using System;
using Game.Characters.Face;
using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>Maps an emotion name used in scenario JSON to a face expression asset.</summary>
    [Serializable]
    public sealed class EmotionBinding
    {
        [SerializeField] private string _emotion = string.Empty;
        [SerializeField] private FaceExpression _expression;

        public string Emotion => _emotion;
        public FaceExpression Expression => _expression;
    }
}
