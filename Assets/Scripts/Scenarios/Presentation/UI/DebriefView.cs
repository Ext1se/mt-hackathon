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
            while (_rows.Count < decisions.Count)
            {
                _rows.Add(Instantiate(_rowPrefab, _decisionsContainer));
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool isUsed = i < decisions.Count;
                _rows[i].gameObject.SetActive(isUsed);
                if (isUsed)
                {
                    _rows[i].Bind(decisions[i], labels);
                }
            }
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
        }
    }
}
