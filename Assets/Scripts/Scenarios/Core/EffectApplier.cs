using System;
using System.Collections.Generic;
using Game.Scenarios.Core.Data;

namespace Game.Scenarios.Core
{
    /// <summary>Applies effects in order; each effect's conditions see the changes made by the previous ones.</summary>
    public static class EffectApplier
    {
        /// <param name="applied">Receives every non-zero change; may be null.</param>
        public static void Apply(IReadOnlyList<EffectData> effects, ScenarioState state, List<AppliedEffect> applied)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                EffectData effect = effects[i];
                if (!ConditionEvaluator.IsMet(effect.Conditions, state))
                {
                    continue;
                }

                int before = state.Get(effect.Key);
                switch (effect.Op)
                {
                    case EffectOps.Add:
                        state.Add(effect.Key, effect.Value);
                        break;
                    case EffectOps.Set:
                        state.Set(effect.Key, effect.Value);
                        break;
                    default:
                        throw new ArgumentException($"Unknown effect operator '{effect.Op}' for key '{effect.Key}'.");
                }

                int delta = state.Get(effect.Key) - before;
                if (delta != 0 && applied != null)
                {
                    applied.Add(new AppliedEffect(effect.Key, delta));
                }
            }
        }
    }
}
