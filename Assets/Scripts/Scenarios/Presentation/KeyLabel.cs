using System;
using UnityEngine;

namespace Game.Scenarios.Presentation
{
    /// <summary>Display name for a state key.</summary>
    [Serializable]
    public sealed class KeyLabel
    {
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private string _label = string.Empty;

        public string Key => _key;
        public string Label => _label;
    }
}
