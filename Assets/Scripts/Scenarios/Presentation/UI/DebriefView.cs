using System;
using System.Collections.Generic;
using System.Text;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Post-scenario screen: ending, grades, competencies and every decision with its feedback.</summary>
    public sealed class DebriefView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _endingTitle;
        [SerializeField] private TMP_Text _endingText;
        [SerializeField] private TMP_Text _scales;
        [SerializeField] private TMP_Text _competencies;
        [SerializeField] private RectTransform _decisionsContainer;
        [SerializeField] private DecisionRow _rowPrefab;
        [SerializeField] private Button _closeButton;
        [Tooltip("{0} loyalty, {1} loyalty grade, {2} safety, {3} safety grade. The words live here, not in code.")]
        [SerializeField] private string _scalesFormat = "{0} ({1}) / {2} ({3})";

        private readonly List<DecisionRow> _rows = new List<DecisionRow>();
        private readonly StringBuilder _builder = new StringBuilder();

        public event Action Closed;

        private void Awake()
        {
            _closeButton.onClick.AddListener(OnCloseClicked);
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        public void Show(ScenarioResult result, ScenarioLabels labels)
        {
            _root.SetActive(true);
            _endingTitle.text = result.EndingTitle;
            _endingText.text = result.EndingText;
            _scales.text = string.Format(_scalesFormat, result.Loyalty, labels.GetGradeLabel(result.LoyaltyGrade),
                result.Safety, labels.GetGradeLabel(result.SafetyGrade));
            _competencies.text = BuildCompetencies(result, labels);
            ShowDecisions(result.Decisions, labels);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        private string BuildCompetencies(ScenarioResult result, ScenarioLabels labels)
        {
            _builder.Clear();
            foreach (KeyValuePair<string, int> pair in result.Competencies)
            {
                if (_builder.Length > 0)
                {
                    _builder.AppendLine();
                }

                _builder.Append(labels.GetLabel(pair.Key)).Append(": ");
                if (pair.Value > 0)
                {
                    _builder.Append('+');
                }

                _builder.Append(pair.Value);
            }

            return _builder.ToString();
        }

        private void ShowDecisions(IReadOnlyList<DecisionRecord> decisions, ScenarioLabels labels)
        {
            int shown = 0;
            for (int i = 0; i < decisions.Count; i++)
            {
                if (!IsWorthShowing(decisions[i], labels))
                {
                    continue;
                }

                if (shown == _rows.Count)
                {
                    _rows.Add(Instantiate(_rowPrefab, _decisionsContainer));
                }

                _rows[shown].gameObject.SetActive(true);
                _rows[shown].Bind(decisions[i], labels);
                shown++;
            }

            for (int i = shown; i < _rows.Count; i++)
            {
                _rows[i].gameObject.SetActive(false);
            }
        }

        // Navigation and dialogue glue ("Continue", "Step away", a player's question that leads on) teach nothing:
        // a row is shown only with feedback, a visible scale change, a reference mark or a timeout.
        private static bool IsWorthShowing(DecisionRecord record, ScenarioLabels labels)
        {
            if (record.IsReference || record.TimedOut || !string.IsNullOrEmpty(record.Feedback))
            {
                return true;
            }

            foreach (AppliedEffect effect in record.Effects)
            {
                if (labels.TryGetLabel(effect.Key, out string _))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
        }
    }
}
