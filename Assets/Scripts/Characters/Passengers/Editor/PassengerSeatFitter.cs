using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Fits a sitting clip to a seat by coordinate descent over a few pose parameters (body recline and offset, leg and arm
    /// muscles), scoring each candidate with <see cref="PassengerSeatFitProbe"/> on one or more characters.
    /// Parameters, in order: body pitch (degrees, negative reclines), body offset up and forward (metres),
    /// then offsets for both sides of Upper Leg Front-Back, Lower Leg Stretch, Upper Leg In-Out, Arm Down-Up, Arm Front-Back,
    /// Forearm Stretch, Hand Down-Up, Arm Twist In-Out, Forearm Twist In-Out, Hand In-Out. Always fit and apply from the
    /// unmodified clip: it is also the default reference for where the hands rest.
    /// </summary>
    public static class PassengerSeatFitter
    {
        public const int ParameterCount = 13;

        private const float TargetHipGap = 0.015f;
        private const float MaxFootHeight = 0.03f;
        private const float MinFootHeight = -0.01f;
        private const float TargetBackGap = 0.04f;
        private const float HandDriftTolerance = 0.015f;
        private const float MaxHandDriftTolerance = 0.04f;
        // Knee and ankle thickness with trousers, added to the joint position for the lane check.
        private const float LegRadius = 0.08f;
        // Joint centres closer than this put one knee (with trousers) or shoe into the other.
        private const float MinKneeGap = 0.15f;
        private const float MinAnkleGap = 0.13f;

        /// <summary>
        /// Half the seat pitch on a bench. When above zero, knees and ankles are kept within it so that neighbours'
        /// legs never overlap. Set before calling <see cref="Fit"/> or <see cref="Cost"/>; 0 disables the check.
        /// </summary>
        public static float LaneHalfWidth;

        // Hand points relative to the thighs in the unmodified clip, per character, clip and sample count.
        private static readonly Dictionary<(int Character, int Clip, int Samples), Vector3[]> s_handReference =
            new Dictionary<(int Character, int Clip, int Samples), Vector3[]>();

        public static readonly string[] ParameterNames =
        {
            "Pitch", "OffsetY", "OffsetZ", "UpperLegFrontBack", "LowerLegStretch", "UpperLegInOut", "ArmDownUp", "ArmFrontBack",
            "ForearmStretch", "HandDownUp", "ArmTwist", "ForearmTwist", "HandInOut"
        };

        private static readonly float[] s_initialSteps =
        {
            4f, 0.02f, 0.03f, 0.08f, 0.1f, 0.05f, 0.08f, 0.08f, 0.08f, 0.1f, 0.1f, 0.1f, 0.1f
        };

        private static readonly string[] s_muscles =
        {
            "Left Upper Leg Front-Back", "Right Upper Leg Front-Back",
            "Left Lower Leg Stretch", "Right Lower Leg Stretch",
            "Left Upper Leg In-Out", "Right Upper Leg In-Out",
            "Left Arm Down-Up", "Right Arm Down-Up",
            "Left Arm Front-Back", "Right Arm Front-Back",
            "Left Forearm Stretch", "Right Forearm Stretch",
            "Left Hand Down-Up", "Right Hand Down-Up",
            "Left Arm Twist In-Out", "Right Arm Twist In-Out",
            "Left Forearm Twist In-Out", "Right Forearm Twist In-Out",
            "Left Hand In-Out", "Right Hand In-Out"
        };

        /// <summary>Returns a copy of <paramref name="source"/> with the parameters applied; destroy it when done.</summary>
        public static AnimationClip CreateAdjusted(AnimationClip source, float[] parameters)
        {
            AnimationClip clip = Object.Instantiate(source);
            clip.name = source.name;
            Apply(clip, parameters);
            return clip;
        }

        /// <summary>Writes the parameters into <paramref name="clip"/> (relative to its current curves).</summary>
        public static void Apply(AnimationClip clip, float[] parameters)
        {
            if (Mathf.Abs(parameters[0]) > 0.001f)
            {
                PassengerSeatFitProbe.RotateBody(clip, Quaternion.Euler(parameters[0], 0f, 0f));
            }

            PassengerSeatFitProbe.OffsetCurves(clip, new[] { "RootT.y", "RootT.z" }, new[] { parameters[1], parameters[2] });

            float[] muscleOffsets = new float[s_muscles.Length];
            for (int i = 0; i < s_muscles.Length; i++)
            {
                muscleOffsets[i] = parameters[3 + i / 2];
            }

            PassengerSeatFitProbe.OffsetCurves(clip, s_muscles, muscleOffsets);
        }

        /// <summary>Scores the parameters on every character (lower is better) and describes the worst problems.</summary>
        public static float Cost(GameObject[] characters, AnimationClip source, MeshCollider seat, float[] parameters,
            int samples, int vertexStep, out string summary, bool matchHands = true)
        {
            AnimationClip clip = CreateAdjusted(source, parameters);
            float cost = 0f;
            StringBuilder text = new StringBuilder();
            try
            {
                foreach (GameObject character in characters)
                {
                    Vector3[] handReference = matchHands ? GetHandReference(character, source, samples) : null;
                    SeatFitResult result = PassengerSeatFitProbe.Measure(character, clip, seat, samples, vertexStep,
                        handReference: handReference);
                    cost += Score(result);
                    text.Append(result.Format());
                }
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }

            summary = text.ToString();
            return cost;
        }

        /// <summary>
        /// Coordinate descent from <paramref name="start"/>: tries a step up and down for every parameter, keeps
        /// improvements and halves the steps when a full sweep finds none. Stops after the evaluation budget.
        /// Fit the body with <paramref name="matchHands"/> off and the arms locked, then place the hands with
        /// <see cref="PassengerArmFitter"/>: the hand reference only fights the body fit.
        /// </summary>
        public static float[] Fit(GameObject[] characters, AnimationClip source, MeshCollider seat, float[] start,
            int maxEvaluations, int samples, int vertexStep, bool[] locked, out string log, bool matchHands = true)
        {
            float[] best = (float[])start.Clone();
            float[] steps = (float[])s_initialSteps.Clone();
            float bestCost = Cost(characters, source, seat, best, samples, vertexStep, out _, matchHands);
            int evaluations = 1;
            StringBuilder history = new StringBuilder();
            history.AppendLine($"start cost={bestCost:F1} {Describe(best)}");

            while (evaluations < maxEvaluations)
            {
                bool improved = false;
                for (int i = 0; i < ParameterCount && evaluations < maxEvaluations; i++)
                {
                    if (locked != null && locked[i])
                    {
                        continue;
                    }

                    for (int direction = -1; direction <= 1 && evaluations < maxEvaluations; direction += 2)
                    {
                        float[] candidate = (float[])best.Clone();
                        candidate[i] += direction * steps[i];
                        float cost = Cost(characters, source, seat, candidate, samples, vertexStep, out _, matchHands);
                        evaluations++;
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            best = candidate;
                            improved = true;
                            history.AppendLine($"#{evaluations} cost={bestCost:F1} {Describe(best)}");
                            break;
                        }
                    }
                }

                if (!improved)
                {
                    for (int i = 0; i < steps.Length; i++)
                    {
                        steps[i] *= 0.5f;
                    }
                }
            }

            log = history.ToString();
            return best;
        }

        public static string Describe(float[] parameters)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                text.Append($"{ParameterNames[i]}={parameters[i]:0.###} ");
            }

            return text.ToString();
        }

        private static Vector3[] GetHandReference(GameObject character, AnimationClip source, int samples)
        {
            (int, int, int) key = (character.GetInstanceID(), source.GetInstanceID(), samples);
            if (!s_handReference.TryGetValue(key, out Vector3[] reference))
            {
                reference = PassengerSeatFitProbe.SampleHandReference(character, source, samples);
                s_handReference[key] = reference;
            }

            return reference;
        }

        private static float Score(SeatFitResult result)
        {
            float samples = Mathf.Max(1, result.Samples);
            float score = result.InsideDepthSum * 100f / samples + result.TotalNear * 0.05f / samples;

            if (result.HipGap > TargetHipGap)
            {
                score += (result.HipGap - TargetHipGap) * 1000f;
            }

            if (result.FootHeight > MaxFootHeight)
            {
                score += (result.FootHeight - MaxFootHeight) * 1000f;
            }
            else if (result.FootHeight < MinFootHeight)
            {
                score += (MinFootHeight - result.FootHeight) * 1000f;
            }

            if (result.BackGap > TargetBackGap)
            {
                score += (result.BackGap - TargetBackGap) * 1500f;
            }

            score += Mathf.Max(0f, MinKneeGap - result.KneeGap) * 3000f;
            score += Mathf.Max(0f, MinAnkleGap - result.AnkleGap) * 3000f;

            if (LaneHalfWidth > 0f)
            {
                score += Mathf.Max(0f, result.LegHalfWidth + LegRadius - LaneHalfWidth) * 3000f;
            }

            score += Mathf.Max(0f, result.HandDrift - HandDriftTolerance) * 1500f;
            score += Mathf.Max(0f, result.MaxHandDrift - MaxHandDriftTolerance) * 500f;

            return score;
        }
    }
}
