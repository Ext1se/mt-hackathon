using System.Collections.Generic;
using System.Text;
using Game.Scenarios.Core;
using Game.Scenarios.Presentation.World;
using TMPro;
using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Lists the current world objectives and highlights their targets in the scene. One objective can be selected
    /// for the guide trail; the selection survives objective list changes while the objective is still there.
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectives;
        [Tooltip("Optional: key hint for switching the guided objective.")]
        [SerializeField] private TMP_Text _guideHint;
        [SerializeField] private Color _selectedColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        [Tooltip("Must exist in the UI font: MoscowSans has no geometric arrows.")]
        [SerializeField] private string _selectedMark = "» ";
        [SerializeField] private string _mark = "• ";

        private readonly StringBuilder _builder = new StringBuilder();
        private readonly List<string> _entries = new List<string>();
        private readonly List<List<string>> _entryTargets = new List<List<string>>();
        private string _selectedObjective;
        private bool _isGuideOff;

        /// <summary>Raised when the guided objective changes, including when the list is rebuilt.</summary>
        public event System.Action SelectionChanged;

        /// <summary>Target ids of the guided objective; empty when nothing is guided.</summary>
        public IReadOnlyList<string> SelectedTargets
        {
            get
            {
                int index = SelectedIndex;
                return index >= 0 ? _entryTargets[index] : (IReadOnlyList<string>)System.Array.Empty<string>();
            }
        }

        private int SelectedIndex => _selectedObjective == null ? -1 : _entries.IndexOf(_selectedObjective);

        private void Awake()
        {
            _root.SetActive(false);
        }

        public void Show(IReadOnlyList<OptionView> options, IReadOnlyList<ScenarioInteractable> targets)
        {
            SetAllHighlighted(targets, false);
            _entries.Clear();
            _entryTargets.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                OptionView option = options[i];
                if (!option.IsWorld || option.IsQuiet)
                {
                    continue;
                }

                Highlight(targets, option.Target);

                // Several targets may share one objective ("find the owner"); it is listed once.
                int entry = _entries.IndexOf(option.Objective);
                if (entry < 0)
                {
                    _entries.Add(option.Objective);
                    _entryTargets.Add(new List<string>());
                    entry = _entries.Count - 1;
                }

                _entryTargets[entry].Add(option.Target);
            }

            // Dialogue nodes have no world objectives; the choice made in the hub is kept for the way back.
            if (_entries.Count > 0 && SelectedIndex < 0)
            {
                _selectedObjective = _isGuideOff || _entries.Count == 0 ? null : _entries[0];
            }

            Render();
            SelectionChanged?.Invoke();
        }

        public void Clear(IReadOnlyList<ScenarioInteractable> targets)
        {
            SetAllHighlighted(targets, false);
            _entries.Clear();
            _entryTargets.Clear();
            _root.SetActive(false);
            SelectionChanged?.Invoke();
        }

        /// <summary>Guides to the next objective; after the last one the guide turns off, then starts over.</summary>
        public void SelectNext()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            int next = SelectedIndex + 1;
            _isGuideOff = next >= _entries.Count;
            _selectedObjective = _isGuideOff ? null : _entries[next];
            Render();
            SelectionChanged?.Invoke();
        }

        /// <summary>Forgets the selection and turns the guide back on for the next scenario.</summary>
        public void ResetSelection()
        {
            _selectedObjective = null;
            _isGuideOff = false;
        }

        private void Render()
        {
            _builder.Clear();
            int selected = SelectedIndex;
            string selectedColor = ColorUtility.ToHtmlStringRGB(_selectedColor);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0)
                {
                    _builder.AppendLine();
                }

                if (i == selected)
                {
                    _builder.Append("<color=#").Append(selectedColor).Append('>').Append(_selectedMark).Append(_entries[i]).Append("</color>");
                }
                else
                {
                    _builder.Append(_mark).Append(_entries[i]);
                }
            }

            _root.SetActive(_entries.Count > 0);
            _objectives.text = _builder.ToString();
            if (_guideHint != null)
            {
                _guideHint.gameObject.SetActive(_entries.Count > 0);
            }
        }

        private static void Highlight(IReadOnlyList<ScenarioInteractable> targets, string targetId)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].TargetId == targetId)
                {
                    targets[i].SetHighlighted(true);
                }
            }
        }

        private static void SetAllHighlighted(IReadOnlyList<ScenarioInteractable> targets, bool isHighlighted)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetHighlighted(isHighlighted);
            }
        }
    }
}
