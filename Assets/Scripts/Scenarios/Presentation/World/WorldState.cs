using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>A change of the scene that happens once, when its scenario conditions first hold.</summary>
    [Serializable]
    public sealed class WorldState
    {
        [SerializeField] private string _id = string.Empty;
        [Tooltip("Scenario conditions as JSON, in the scenario file format: AND inside a group, OR between groups.")]
        [SerializeField] private string _conditions = "[]";
        [Tooltip("On: the screen fades to black while the change happens. Off: the change is instant (e.g. at scenario start).")]
        [SerializeField] private bool _fade = true;
        [SerializeField] private List<WorldAction> _actions = new List<WorldAction>();

        public string Id => _id;
        public string Conditions => _conditions;
        public bool Fade => _fade;
        public IReadOnlyList<WorldAction> Actions => _actions;
    }
}
