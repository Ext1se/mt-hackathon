using System.Collections.Generic;
using Game.Scenarios.Core.Data;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// Static checks for hand-written scenarios: dangling jumps, timers without a timeout outcome,
    /// unknown operators, misplaced world options, missing fallback ending.
    /// </summary>
    public static class ScenarioValidator
    {
        public static List<string> Validate(ScenarioData data)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrEmpty(data.Id))
            {
                errors.Add("Scenario id is empty.");
            }

            Dictionary<string, NodeData> nodes = new Dictionary<string, NodeData>();
            foreach (NodeData node in data.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    errors.Add("A node has an empty id.");
                }
                else if (nodes.ContainsKey(node.Id))
                {
                    errors.Add($"Duplicate node id '{node.Id}'.");
                }
                else
                {
                    nodes.Add(node.Id, node);
                }
            }

            if (!nodes.ContainsKey(data.Start))
            {
                errors.Add($"Start node '{data.Start}' does not exist.");
            }

            foreach (NodeData node in data.Nodes)
            {
                ValidateNode(node, nodes, errors);
            }

            ValidateTriggers(data, nodes, errors);
            ValidateEndings(data, errors);
            ValidateVariants(data, errors);
            return errors;
        }

        private static void ValidateNode(NodeData node, Dictionary<string, NodeData> nodes, List<string> errors)
        {
            string where = $"Node '{node.Id}'";
            bool isHub = node.Kind == NodeKinds.Hub;
            bool isRoam = node.Kind == NodeKinds.Roam;
            if (!isHub && !isRoam && node.Kind != NodeKinds.Choice)
            {
                errors.Add($"{where}: unknown kind '{node.Kind}'.");
            }

            ValidateLines(node.Lines, where, errors);
            ValidateEffects(node.OnEnter, where, errors);
            foreach (HintData hint in node.Hints)
            {
                ValidateConditions(hint.Conditions, $"{where}, hint", errors);
            }

            HashSet<string> optionIds = new HashSet<string>();
            foreach (OptionData option in node.Options)
            {
                string optionWhere = $"{where}, option '{option.Id}'";
                if (string.IsNullOrEmpty(option.Id) || !optionIds.Add(option.Id))
                {
                    errors.Add($"{optionWhere}: empty or duplicate option id.");
                }

                if (!option.Hidden && string.IsNullOrEmpty(option.Text))
                {
                    errors.Add($"{optionWhere}: a visible option needs text.");
                }

                bool isWorld = !string.IsNullOrEmpty(option.Target);
                if (isRoam && !option.Hidden && !isWorld)
                {
                    errors.Add($"{optionWhere}: roam nodes only accept options with a target.");
                }

                if (!isRoam && isWorld)
                {
                    errors.Add($"{optionWhere}: options with a target are only allowed in roam nodes.");
                }

                if (!isHub && string.IsNullOrEmpty(option.Next) && option.NextRules.Count == 0)
                {
                    errors.Add($"{optionWhere}: no next node.");
                }

                CheckTarget(option.Next, nodes, optionWhere, errors);
                ValidateRules(option.NextRules, nodes, optionWhere, errors);
                ValidateConditions(option.Conditions, optionWhere, errors);
                ValidateEffects(option.Effects, optionWhere, errors);
                ValidateLines(option.Lines, optionWhere, errors);
            }

            if (node.TimeLimit > 0f && !isHub && !optionIds.Contains(node.TimeoutOption))
            {
                errors.Add($"{where}: a timed node needs an existing timeoutOption.");
            }

            if (isHub)
            {
                if (node.MaxActions <= 0)
                {
                    errors.Add($"{where}: a hub needs maxActions > 0.");
                }

                if (string.IsNullOrEmpty(node.ExitNext))
                {
                    errors.Add($"{where}: a hub needs exitNext.");
                }

                CheckTarget(node.ExitNext, nodes, where, errors);
                ValidateRules(node.Exit, nodes, where, errors);
                ValidateRules(node.Leave, nodes, where, errors);
            }
        }

        private static void ValidateTriggers(ScenarioData data, Dictionary<string, NodeData> nodes, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (TriggerData trigger in data.Triggers)
            {
                string where = $"Trigger '{trigger.Id}'";
                if (string.IsNullOrEmpty(trigger.Id) || !ids.Add(trigger.Id))
                {
                    errors.Add($"{where}: empty or duplicate trigger id.");
                }

                if (!nodes.ContainsKey(trigger.Node))
                {
                    errors.Add($"{where}: target '{trigger.Node}' does not exist.");
                }

                if (!string.IsNullOrEmpty(trigger.Hub)
                    && (!nodes.TryGetValue(trigger.Hub, out NodeData hub) || hub.Kind != NodeKinds.Hub))
                {
                    errors.Add($"{where}: hub '{trigger.Hub}' is not a hub node.");
                }

                ValidateConditions(trigger.Conditions, where, errors);
            }
        }

        private static void ValidateEndings(ScenarioData data, List<string> errors)
        {
            if (data.Endings.Count == 0)
            {
                errors.Add("The scenario has no endings.");
                return;
            }

            foreach (EndingData ending in data.Endings)
            {
                string where = $"Ending '{ending.Id}'";
                if (string.IsNullOrEmpty(ending.Id))
                {
                    errors.Add("An ending has an empty id.");
                }

                ValidateConditions(ending.Conditions, where, errors);
                ValidateLines(ending.Lines, where, errors);
            }

            if (data.Endings[data.Endings.Count - 1].Conditions.Count > 0)
            {
                errors.Add("The last ending must have no conditions: it is the fallback.");
            }
        }

        private static void ValidateVariants(ScenarioData data, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (VariantData variant in data.Variants)
            {
                if (string.IsNullOrEmpty(variant.Id) || !ids.Add(variant.Id))
                {
                    errors.Add($"Variant '{variant.Id}': empty or duplicate id.");
                }

                if (variant.Weight <= 0)
                {
                    errors.Add($"Variant '{variant.Id}': weight must be positive.");
                }
            }
        }

        private static void ValidateRules(IReadOnlyList<NextRuleData> rules, Dictionary<string, NodeData> nodes, string where,
            List<string> errors)
        {
            foreach (NextRuleData rule in rules)
            {
                if (string.IsNullOrEmpty(rule.Node))
                {
                    errors.Add($"{where}: a rule has no node.");
                }

                CheckTarget(rule.Node, nodes, where, errors);
                ValidateConditions(rule.Conditions, where, errors);
            }
        }

        private static void ValidateLines(IReadOnlyList<LineData> lines, string where, List<string> errors)
        {
            foreach (LineData line in lines)
            {
                ValidateConditions(line.Conditions, where, errors);
            }
        }

        private static void ValidateConditions(IReadOnlyList<ConditionData> conditions, string where, List<string> errors)
        {
            foreach (ConditionData condition in conditions)
            {
                if (string.IsNullOrEmpty(condition.Key))
                {
                    errors.Add($"{where}: a condition has an empty key.");
                }

                if (!ConditionOps.IsKnown(condition.Op))
                {
                    errors.Add($"{where}: unknown operator '{condition.Op}'.");
                }
            }
        }

        private static void ValidateEffects(IReadOnlyList<EffectData> effects, string where, List<string> errors)
        {
            foreach (EffectData effect in effects)
            {
                if (string.IsNullOrEmpty(effect.Key))
                {
                    errors.Add($"{where}: an effect has an empty key.");
                }

                if (!EffectOps.IsKnown(effect.Op))
                {
                    errors.Add($"{where}: unknown operator '{effect.Op}'.");
                }

                ValidateConditions(effect.Conditions, where, errors);
            }
        }

        private static void CheckTarget(string target, Dictionary<string, NodeData> nodes, string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(target) || NextTargets.IsSpecial(target))
            {
                return;
            }

            if (!nodes.ContainsKey(target))
            {
                errors.Add($"{where}: target '{target}' does not exist.");
            }
        }
    }
}
