using System.Collections.Generic;
using System.Text;
using Game.Scenarios.Core;
using Game.Scenarios.Presentation.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Lists the current world objectives and highlights their targets in the scene. One objective can be selected
    /// for the guide trail, by click or by key; the selection survives objective list changes while the objective is
    /// still there. Objectives whose goal is reached stay listed, crossed out.
    /// </summary>
    public sealed class QuestTracker : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectives;
        [Tooltip("Optional: key hint for switching the guided objective.")]
        [SerializeField] private TMP_Text _guideHint;
        [SerializeField] private Color _selectedColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        [Tooltip("Must exist in the UI font: MoscowSans has no geometric arrows.")]
        [SerializeField] private string _selectedMark = "» ";
        [SerializeField] private string _mark = "• ";
        [Tooltip("Colour of objectives already done; they are also crossed out.")]
        [SerializeField] private Color _doneColor = new Color(0.55f, 0.58f, 0.63f, 1f);

        private readonly StringBuilder _builder = new StringBuilder();
        private readonly List<string> _entries = new List<string>();
        private readonly List<List<string>> _entryTargets = new List<List<string>>();
        private readonly List<bool> _entryDone = new List<bool>();
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
            // The list is one text; a click finds the line under the pointer by its link.
            _objectives.raycastTarget = true;
            _root.SetActive(false);
        }

        public void Show(IReadOnlyList<OptionView> options, IReadOnlyList<ScenarioInteractable> targets)
        {
            SetAllHighlighted(targets, false);
            _entries.Clear();
            _entryTargets.Clear();
            _entryDone.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                OptionView option = options[i];
                if (!option.IsWorld || option.IsQuiet)
                {
                    continue;
                }

                // Markers only over what is still to do.
                if (!option.IsDone)
                {
                    Highlight(targets, option.Target);
                }

                // Several targets may share one objective ("find the owner"); it is listed once, done when all are.
                int entry = _entries.IndexOf(option.Objective);
                if (entry < 0)
                {
                    _entries.Add(option.Objective);
                    _entryTargets.Add(new List<string>());
                    _entryDone.Add(true);
                    entry = _entries.Count - 1;
                }

                _entryTargets[entry].Add(option.Target);
                _entryDone[entry] = _entryDone[entry] && option.IsDone;
            }

            // Dialogue nodes have no world objectives; the choice made in the hub is kept for the way back.
            // A guided objective that just got done hands the guide to the next open one.
            int selected = SelectedIndex;
            if (_entries.Count > 0 && !_isGuideOff && (selected < 0 || _entryDone[selected]))
            {
                _selectedObjective = FirstOpenEntry();
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

        /// <summary>A click on an objective makes it the guided one.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            int link = TMP_TextUtilities.FindIntersectingLink(_objectives, eventData.position, eventData.pressEventCamera);
            if (link < 0 || !int.TryParse(_objectives.textInfo.linkInfo[link].GetLinkID(), out int index)
                || index < 0 || index >= _entries.Count)
            {
                return;
            }

            _isGuideOff = false;
            _selectedObjective = _entries[index];
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
            string doneColor = ColorUtility.ToHtmlStringRGB(_doneColor);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0)
                {
                    _builder.AppendLine();
                }

                bool isSelected = i == selected;
                bool isDone = _entryDone[i];
                _builder.Append("<link=\"").Append(i).Append("\">");
                if (isSelected || isDone)
                {
                    _builder.Append("<color=#").Append(isSelected ? selectedColor : doneColor).Append('>');
                }

                _builder.Append(isSelected ? _selectedMark : _mark);
                if (isDone)
                {
                    _builder.Append("<s>").Append(_entries[i]).Append("</s>");
                }
                else
                {
                    _builder.Append(_entries[i]);
                }

                if (isSelected || isDone)
                {
                    _builder.Append("</color>");
                }

                _builder.Append("</link>");
            }

            _root.SetActive(_entries.Count > 0);
            _objectives.text = _builder.ToString();
            if (_guideHint != null)
            {
                _guideHint.gameObject.SetActive(_entries.Count > 0);
            }
        }

        private string FirstOpenEntry()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entryDone[i])
                {
                    return _entries[i];
                }
            }

            return _entries.Count > 0 ? _entries[0] : null;
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
