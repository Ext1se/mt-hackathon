using System.Text;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>Result of <see cref="PassengerSeatFitProbe.Measure"/>. Distances are in metres.</summary>
    public class SeatFitResult
    {
        public string Title;
        public int Samples;
        public HumanBodyBones[] Parts;
        public int[] InsideCount;
        public float[] InsideDepth;
        public float[] WorstInsideTime;
        public int[] NearCount;

        /// <summary>Sum of penetration depths of all tested vertices over all samples.</summary>
        public float InsideDepthSum;

        /// <summary>Lowest distance from buttock vertices down to the seat on the first frame; -1 if none hit.</summary>
        public float HipGap;

        /// <summary>Lowest foot vertex above the floor (the character root) on the first frame.</summary>
        public float FootHeight;

        /// <summary>Lowest distance from chest vertices back to the seat on the first frame; -1 if none hit.</summary>
        public float BackGap;

        /// <summary>Largest sideways distance of a knee or ankle joint from the character root over all samples.</summary>
        public float LegHalfWidth;

        /// <summary>Smallest sideways distance right minus left between the knees over all samples (negative means crossed).</summary>
        public float KneeGap;

        /// <summary>Smallest sideways distance right minus left between the ankles over all samples.</summary>
        public float AnkleGap;

        /// <summary>Mean distance of the hand points from their reference place on the thighs; 0 without a reference.</summary>
        public float HandDrift;

        /// <summary>Largest single hand point drift over all samples.</summary>
        public float MaxHandDrift;

        public int TotalInside
        {
            get
            {
                int total = 0;
                for (int i = 0; i < InsideCount.Length; i++)
                {
                    total += InsideCount[i];
                }

                return total;
            }
        }

        public int TotalNear
        {
            get
            {
                int total = 0;
                for (int i = 0; i < NearCount.Length; i++)
                {
                    total += NearCount[i];
                }

                return total;
            }
        }

        public float WorstDepth
        {
            get
            {
                float worst = 0f;
                for (int i = 0; i < InsideDepth.Length; i++)
                {
                    worst = Mathf.Max(worst, InsideDepth[i]);
                }

                return worst;
            }
        }

        public string Format()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine($"{Title}: inside={TotalInside} worst={WorstDepth * 100f:F1}cm near={TotalNear} "
                + $"hipGap={HipGap * 100f:F1}cm footY={FootHeight * 100f:F1}cm backGap={BackGap * 100f:F1}cm "
                + $"handDrift={HandDrift * 100f:F1}cm (max {MaxHandDrift * 100f:F1}cm) legHalfWidth={LegHalfWidth * 100f:F1}cm "
                + $"kneeGap={KneeGap * 100f:F1}cm ankleGap={AnkleGap * 100f:F1}cm");
            for (int i = 0; i < Parts.Length; i++)
            {
                if (InsideCount[i] == 0 && NearCount[i] == 0)
                {
                    continue;
                }

                report.AppendLine($"  {Parts[i]}: inside={InsideCount[i]} depth={InsideDepth[i] * 100f:F1}cm "
                    + $"@{WorstInsideTime[i]:F1}s near={NearCount[i]}");
            }

            return report.ToString();
        }
    }
}
