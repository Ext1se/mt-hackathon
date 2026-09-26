using System.Collections.Generic;
using Game.Scenarios.Core;
using UnityEngine;

namespace Game.Scenarios.Presentation
{
    /// <summary>Player-facing names of scales, competencies, variables and grades. Keeps localized text out of code.</summary>
    [CreateAssetMenu(fileName = "ScenarioLabels", menuName = "Game/Scenarios/Scenario Labels")]
    public sealed class ScenarioLabels : ScriptableObject
    {
        [Tooltip("Keys shown in the debrief. Keys without a label (flags) are not shown.")]
        [SerializeField] private List<KeyLabel> _keys = new List<KeyLabel>();

        [Tooltip("Grade names in the order Fail, Satisfactory, Good, Excellent.")]
        [SerializeField] private string[] _grades = new string[4];

        public bool TryGetLabel(string key, out string label)
        {
            foreach (KeyLabel entry in _keys)
            {
                if (entry.Key == key)
                {
                    label = entry.Label;
                    return true;
                }
            }

            label = null;
            return false;
        }

        public string GetLabel(string key)
        {
            return TryGetLabel(key, out string label) ? label : key;
        }

        public string GetGradeLabel(ScaleGrade grade)
        {
            int index = (int)grade;
            if (index < _grades.Length && !string.IsNullOrEmpty(_grades[index]))
            {
                return _grades[index];
            }

            return grade.ToString();
        }
    }
}
