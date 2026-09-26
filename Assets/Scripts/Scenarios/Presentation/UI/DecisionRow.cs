using System.Text;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>One decision in the debrief: what was chosen, why it mattered, what it changed.</summary>
    public sealed class DecisionRow : MonoBehaviour
    {
        private const string EffectSeparator = " · ";

        [SerializeField] private TMP_Text _choice;
        [SerializeField] private TMP_Text _feedback;
        [SerializeField] private TMP_Text _effects;
        [Tooltip("Shown for the reference answer from the dataset.")]
        [SerializeField] private GameObject _referenceMark;
        [SerializeField] private GameObject _timeoutMark;

        private readonly StringBuilder _builder = new StringBuilder();

        public void Bind(DecisionRecord record, ScenarioLabels labels)
        {
            _choice.text = record.IsConsequence ? record.Prompt : record.OptionText;
            _feedback.text = record.Feedback;
            _feedback.gameObject.SetActive(!string.IsNullOrEmpty(record.Feedback));
            _referenceMark.SetActive(record.IsReference);
            _timeoutMark.SetActive(record.TimedOut);

            _builder.Clear();
            foreach (AppliedEffect effect in record.Effects)
            {
                if (!labels.TryGetLabel(effect.Key, out string label))
                {
                    continue;
                }

                if (_builder.Length > 0)
                {
                    _builder.Append(EffectSeparator);
                }

                _builder.Append(label).Append(' ');
                if (effect.Delta > 0)
                {
                    _builder.Append('+');
                }

                _builder.Append(effect.Delta);
            }

            _effects.text = _builder.ToString();
        }
    }
}
