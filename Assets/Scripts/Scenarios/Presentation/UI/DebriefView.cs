using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Post-scenario screen: ending, grades, competencies and every decision with its feedback. Continue lets the player
    /// walk on in the scene, restart reloads it, menu goes to the main menu.
    /// </summary>
    public sealed class DebriefView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _endingTitle;
        [SerializeField] private TMP_Text _endingText;
        [SerializeField] private TMP_Text _scales;
        [SerializeField] private TMP_Text _competencies;
        [SerializeField] private RectTransform _decisionsContainer;
        [SerializeField] private DecisionRow _rowPrefab;
        [Tooltip("Continue: back to the scene, the scenario is over.")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;
        [Tooltip("Main menu scene; the menu button is hidden when empty.")]
        [SerializeField] private string _menuScene = string.Empty;
        [Tooltip("{0} loyalty, {1} loyalty grade, {2} safety, {3} safety grade. The words live here, not in code.")]
        [SerializeField] private string _scalesFormat = "{0} ({1}) / {2} ({3})";

        private readonly List<DecisionRow> _rows = new List<DecisionRow>();
        private readonly StringBuilder _builder = new StringBuilder();

        public event Action Closed;

        private void Awake()
        {
            _closeButton.onClick.AddListener(OnCloseClicked);
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.AddListener(OnMenuClicked);
                _menuButton.gameObject.SetActive(!string.IsNullOrEmpty(_menuScene));
            }

            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _closeButton.onClick.RemoveListener(OnCloseClicked);
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveListener(OnMenuClicked);
            }
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

        // Time and tweens are global: both are reset before the next scene starts.
        private static void LeaveTo(string scene)
        {
            Time.timeScale = 1f;
            DOTween.KillAll();
            SceneManager.LoadScene(scene);
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
        }

        private void OnRestartClicked()
        {
            LeaveTo(SceneManager.GetActiveScene().path);
        }

        private void OnMenuClicked()
        {
            LeaveTo(_menuScene);
        }
    }
}
