using System.Collections.Generic;
using System.Text;
using Game.Scenarios.Core;
using Game.Scenarios.Presentation.World;
using TMPro;
using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Lists the current world objectives and highlights their targets in the scene.</summary>
    public sealed class QuestTracker : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectives;

        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            _root.SetActive(false);
        }

        public void Show(IReadOnlyList<OptionView> options, IReadOnlyList<ScenarioInteractable> targets)
        {
            _builder.Clear();
            SetAllHighlighted(targets, false);
            for (int i = 0; i < options.Count; i++)
            {
                OptionView option = options[i];
                if (!option.IsWorld)
                {
                    continue;
                }

                Highlight(targets, option.Target);
                if (IsListed(options, i))
                {
                    continue;
                }

                if (_builder.Length > 0)
                {
                    _builder.AppendLine();
                }

                _builder.Append("• ").Append(option.Objective);
            }

            _root.SetActive(_builder.Length > 0);
            _objectives.text = _builder.ToString();
        }

        public void Clear(IReadOnlyList<ScenarioInteractable> targets)
        {
            SetAllHighlighted(targets, false);
            _root.SetActive(false);
        }

        // Several targets may share one objective ("find the owner"); it is listed once.
        private static bool IsListed(IReadOnlyList<OptionView> options, int index)
        {
            for (int i = 0; i < index; i++)
            {
                if (options[i].IsWorld && options[i].Objective == options[index].Objective)
                {
                    return true;
                }
            }

            return false;
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
