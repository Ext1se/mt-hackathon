using System;
using System.Collections.Generic;
using Game.Scenarios.Core.Data;

namespace Game.Scenarios.Core
{
    /// <summary>Evaluates condition lists: AND inside a group, OR between groups. Allocation-free.</summary>
    public static class ConditionEvaluator
    {
        public static bool IsMet(IReadOnlyList<ConditionData> conditions, ScenarioState state)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (IsFirstOfGroup(conditions, i) && IsGroupMet(conditions, conditions[i].Group, state))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Compare(int actual, string op, int expected)
        {
            switch (op)
            {
                case ConditionOps.Eq:
                    return actual == expected;
                case ConditionOps.Ne:
                    return actual != expected;
                case ConditionOps.Gt:
                    return actual > expected;
                case ConditionOps.Ge:
                    return actual >= expected;
                case ConditionOps.Lt:
                    return actual < expected;
                case ConditionOps.Le:
                    return actual <= expected;
                default:
                    throw new ArgumentException($"Unknown condition operator '{op}'.");
            }
        }

        private static bool IsFirstOfGroup(IReadOnlyList<ConditionData> conditions, int index)
        {
            int group = conditions[index].Group;
            for (int i = 0; i < index; i++)
            {
                if (conditions[i].Group == group)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsGroupMet(IReadOnlyList<ConditionData> conditions, int group, ScenarioState state)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                ConditionData condition = conditions[i];
                if (condition.Group == group && !Compare(state.Get(condition.Key), condition.Op, condition.Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
