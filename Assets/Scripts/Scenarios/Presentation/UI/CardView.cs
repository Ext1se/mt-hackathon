using System;
using System.Collections.Generic;
using System.Text;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// A centred info panel over a dimmed screen: a title, headed sections (a paragraph and/or a bulleted list) and the
    /// node's options as buttons, e.g. "Problem" and "Recommended actions" after the first conversation.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [Tooltip("Inactive template with two texts, heading then body; cloned once per section.")]
        [SerializeField] private GameObject _sectionTemplate;
        [SerializeField] private RectTransform _optionsContainer;
        [SerializeField] private OptionButton _optionPrefab;
        [SerializeField] private string _bullet = "• ";

        private readonly List<GameObject> _sections = new List<GameObject>();
        private readonly List<OptionButton> _buttons = new List<OptionButton>();
        private readonly StringBuilder _builder = new StringBuilder();

        public event Action<string> OptionChosen;

        public bool IsShown => _root.activeSelf;

        private void Awake()
        {
            _sectionTemplate.SetActive(false);
            _root.SetActive(false);
        }

        public void Show(CardData card, IReadOnlyList<OptionView> options)
        {
            _root.SetActive(true);
            _title.text = card.Title;
            _title.gameObject.SetActive(!string.IsNullOrEmpty(card.Title));
            ShowSections(card.Sections);
            ShowOptions(options);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        private static void Deselect()
        {
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(null);
            }
        }

        private void ShowSections(IReadOnlyList<CardSectionData> sections)
        {
            while (_sections.Count < sections.Count)
            {
                GameObject copy = Instantiate(_sectionTemplate, _sectionTemplate.transform.parent);
                // Sections go in order, right before the buttons.
                copy.transform.SetSiblingIndex(_optionsContainer.GetSiblingIndex());
                _sections.Add(copy);
            }

            for (int i = 0; i < _sections.Count; i++)
            {
                bool isUsed = i < sections.Count;
                _sections[i].SetActive(isUsed);
                if (isUsed)
                {
                    TMP_Text[] texts = _sections[i].GetComponentsInChildren<TMP_Text>(true);
                    texts[0].text = sections[i].Title;
                    texts[1].text = BuildBody(sections[i]);
                }
            }
        }

        private string BuildBody(CardSectionData section)
        {
            _builder.Clear();
            if (!string.IsNullOrEmpty(section.Text))
            {
                _builder.Append(section.Text);
            }

            for (int i = 0; i < section.Items.Count; i++)
            {
                if (_builder.Length > 0)
                {
                    _builder.Append('\n');
                }

                _builder.Append(_bullet).Append(section.Items[i]);
            }

            return _builder.ToString();
        }

        private void ShowOptions(IReadOnlyList<OptionView> options)
        {
            while (_buttons.Count < options.Count)
            {
                _buttons.Add(Instantiate(_optionPrefab, _optionsContainer));
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool isUsed = i < options.Count;
                _buttons[i].gameObject.SetActive(isUsed);
                if (isUsed)
                {
                    _buttons[i].Bind(options[i], OnOptionClicked);
                }
            }

            Deselect();
        }

        private void OnOptionClicked(string optionId)
        {
            Deselect();
            OptionChosen?.Invoke(optionId);
        }
    }
}
