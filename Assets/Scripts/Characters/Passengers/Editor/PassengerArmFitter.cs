using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    public enum ArmFitMode
    {
        /// <summary>Palms and fingers lie on top of the thighs.</summary>
        RestOnThighs,

        /// <summary>Hands are free (gestures); they only must not sink into the thighs or reach the groin.</summary>
        AvoidThighs
    }

    /// <summary>
    /// Fits the seven arm muscles of <see cref="PassengerSeatFitter"/> (Arm Down-Up … Hand In-Out) after the body has been
    /// fitted to a seat. Arm offsets are regularised towards zero, so the arm keeps the shape of the original clip
    /// (bent elbow, relaxed hand); the hand is not pinned to a point, it only has to lie on the thigh surface.
    /// Solved with Levenberg-Marquardt over a few frames of every character.
    /// </summary>
    public static class PassengerArmFitter
    {
        private const int FirstArmParameter = 6;
        private const int ArmParameterCount = 7;
        private const int ProfileSamples = 11;
        private const float ClothingAllowance = 0.01f;
        private const float GroinFraction = 0.2f;
        // Resting hands keep off the first third of the thigh: closer than that reads as hands in the lap.
        private const float RestMinFraction = 0.45f;
        private const float MinUpDot = 0.75f;
        private const float JacobianStep = 0.03f;

        // Lane of the current Fit call; kept here so the residual helpers match the solver's signature.
        private static float s_laneHalfWidth;

        // Cost of a unit muscle offset in metres of hand error; the elbow (Forearm Stretch) is kept closest to the original.
        // Arm Front-Back is cheap: on a narrow bench elbows go forward instead of out, so the hands reach mid-thigh.
        private static readonly float[] s_regularization = { 0.2f, 0.08f, 0.48f, 0.12f, 0.16f, 0.12f, 0.12f };

        // Clearance above the thigh surface: palm centre, middle, index and little knuckles, index, middle and ring tips.
        private static readonly float[] s_clearance = { 0.025f, 0.02f, 0.02f, 0.02f, 0.01f, 0.01f, 0.01f };

        // Arm thickness around the lane-checked points (upper arm middle, elbow, forearm middle, wrist), sleeves included.
        private static readonly float[] s_armRadius = { 0.06f, 0.055f, 0.05f, 0.045f };
        private const float LaneWeight = 2f;

        // Elbows stay at least this far outside the shoulder joints sideways; closer and the forearm sinks into the torso.
        // The original sitting clips keep them 5–7 cm out.
        private const float MinElbowOutside = 0.03f;
        private const float ElbowWeight = 3f;

        // Closest allowed distance between any joint of the left hand and any joint of the right one: two finger thicknesses.
        private const float MinHandGap = 0.06f;
        private const float HandGapWeight = 3f;

        private static readonly HumanBodyBones[] s_leftHandJoints =
        {
            HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftThumbDistal
        };

        private static readonly HumanBodyBones[] s_rightHandJoints =
        {
            HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightLittleDistal, HumanBodyBones.RightThumbDistal
        };

        /// <summary>
        /// Top surface of the left thigh above its bone axis at 0%, 10% … 100% of the thigh length, measured on the baked body
        /// with the arms raised out of the way (otherwise the hands resting there count as thigh).
        /// </summary>
        public static float[] MeasureThighProfile(GameObject character, AnimationClip source, float[] parameters)
        {
            float[] raised = (float[])parameters.Clone();
            raised[FirstArmParameter] = 1f;
            AnimationClip clip = PassengerSeatFitter.CreateAdjusted(source, raised);
            Animator animator = character.GetComponent<Animator>();
            float[] profile = new float[ProfileSamples];
            List<Vector3> vertices = new List<Vector3>();
            Mesh baked = new Mesh();
            try
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(character, clip, 0f);
                AnimationMode.EndSampling();

                foreach (SkinnedMeshRenderer renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.name.Contains("Body") || !renderer.name.Contains("LOD0"))
                    {
                        continue;
                    }

                    renderer.BakeMesh(baked, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        vertices.Add(toWorld.MultiplyPoint3x4(vertex));
                    }
                }

                Vector3 hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                Vector3 knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
                Vector3 axis = knee - hip;
                float length = axis.magnitude;
                axis /= length;
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                for (int k = 0; k < ProfileSamples; k++)
                {
                    float along = length * k / (ProfileSamples - 1);
                    float top = 0f;
                    foreach (Vector3 vertex in vertices)
                    {
                        Vector3 offset = vertex - hip;
                        float t = Vector3.Dot(offset, axis);
                        if (Mathf.Abs(t - along) > 0.02f)
                        {
                            continue;
                        }

                        Vector3 radial = offset - axis * t;
                        if (radial.magnitude < 0.16f && Vector3.Dot(radial.normalized, up) >= 0.95f)
                        {
                            top = Mathf.Max(top, radial.magnitude);
                        }
                    }

                    profile[k] = top;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(clip);
            }

            return profile;
        }

        /// <summary>
        /// Returns <paramref name="parameters"/> with the arm muscles (indices 6–12) solved for <paramref name="mode"/>;
        /// body parameters are left as they are. Characters must be active and placed on the seat.
        /// With <paramref name="laneHalfWidth"/> above zero the arms also stay within that distance from the seat centre
        /// sideways (half the seat pitch), so neighbours on a bench never overlap whatever clips they play.
        /// </summary>
        public static float[] Fit(GameObject[] characters, AnimationClip source, float[] parameters, ArmFitMode mode,
            int samples, int iterations, out string log, float laneHalfWidth = 0f)
        {
            s_laneHalfWidth = laneHalfWidth;
            float[][] profiles = new float[characters.Length][];
            for (int c = 0; c < characters.Length; c++)
            {
                profiles[c] = MeasureThighProfile(characters[c], source, parameters);
            }

            float[] arms = new float[ArmParameterCount];
            float[] current = Residuals(characters, source, parameters, arms, profiles, mode, samples);
            double error = SquaredSum(current);
            double lambda = 0.1;
            StringBuilder history = new StringBuilder();
            history.AppendLine($"start cost={error:F4}");

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float[][] jacobian = new float[ArmParameterCount][];
                for (int j = 0; j < ArmParameterCount; j++)
                {
                    float[] shifted = (float[])arms.Clone();
                    shifted[j] += JacobianStep;
                    float[] residual = Residuals(characters, source, parameters, shifted, profiles, mode, samples);
                    jacobian[j] = new float[current.Length];
                    for (int i = 0; i < current.Length; i++)
                    {
                        jacobian[j][i] = (residual[i] - current[i]) / JacobianStep;
                    }
                }

                float[] step = SolveDampedNormalEquations(jacobian, current, lambda);
                float[] candidate = new float[ArmParameterCount];
                for (int j = 0; j < ArmParameterCount; j++)
                {
                    candidate[j] = arms[j] + step[j];
                }

                float[] candidateResidual = Residuals(characters, source, parameters, candidate, profiles, mode, samples);
                double candidateError = SquaredSum(candidateResidual);
                if (candidateError < error)
                {
                    arms = candidate;
                    current = candidateResidual;
                    error = candidateError;
                    lambda *= 0.5;
                }
                else
                {
                    lambda *= 4;
                }
            }

            float[] result = (float[])parameters.Clone();
            for (int j = 0; j < ArmParameterCount; j++)
            {
                result[FirstArmParameter + j] = arms[j];
            }

            history.AppendLine($"end cost={error:F4} {PassengerSeatFitter.Describe(result)}");
            log = history.ToString();
            return result;
        }

        private static float[] Residuals(GameObject[] characters, AnimationClip source, float[] parameters, float[] arms,
            float[][] profiles, ArmFitMode mode, int samples)
        {
            float[] full = (float[])parameters.Clone();
            for (int j = 0; j < ArmParameterCount; j++)
            {
                full[FirstArmParameter + j] = arms[j];
            }

            AnimationClip clip = PassengerSeatFitter.CreateAdjusted(source, full);
            List<float> residuals = new List<float>();
            try
            {
                AnimationMode.StartAnimationMode();
                for (int c = 0; c < characters.Length; c++)
                {
                    Animator animator = characters[c].GetComponent<Animator>();
                    for (int s = 0; s < samples; s++)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(characters[c], clip, clip.length * s / samples);
                        AnimationMode.EndSampling();
                        AddHandResiduals(animator, true, profiles[c], mode, residuals);
                        AddHandResiduals(animator, false, profiles[c], mode, residuals);
                        AddHandGapResidual(animator, residuals);
                        AddElbowResidual(animator, true, residuals);
                        AddElbowResidual(animator, false, residuals);
                        if (s_laneHalfWidth > 0f)
                        {
                            AddLaneResiduals(animator, true, residuals);
                            AddLaneResiduals(animator, false, residuals);
                        }
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(clip);
            }

            for (int j = 0; j < ArmParameterCount; j++)
            {
                residuals.Add(s_regularization[j] * arms[j]);
            }

            return residuals.ToArray();
        }

        private static void AddHandResiduals(Animator animator, bool isLeft, float[] profile, ArmFitMode mode, List<float> residuals)
        {
            Vector3 hip = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position;
            Vector3 knee = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
            Vector3 axis = knee - hip;
            float length = axis.magnitude;
            axis /= length;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;

            Vector3 wrist = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
            Vector3 knuckle = Bone(animator, isLeft, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal);
            Vector3[] points =
            {
                (wrist + knuckle) * 0.5f,
                knuckle,
                Bone(animator, isLeft, HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal),
                Bone(animator, isLeft, HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal),
                Bone(animator, isLeft, HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal),
                Bone(animator, isLeft, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal),
                Bone(animator, isLeft, HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal)
            };

            for (int q = 0; q < points.Length; q++)
            {
                Vector3 offset = points[q] - hip;
                float t = Vector3.Dot(offset, axis);
                float fraction = t / length;
                Vector3 radial = offset - axis * t;
                float distance = radial.magnitude;
                float surface = SurfaceAt(profile, fraction) + ClothingAllowance + s_clearance[q];
                float gap = distance - surface;
                bool isOverThigh = fraction > 0.05f && fraction < 1.05f;

                if (mode == ArmFitMode.RestOnThighs)
                {
                    // Sinking into the thigh costs twice as much as hovering above it.
                    residuals.Add(gap < 0f ? gap * 2f : gap);
                    // Strong terms: without them a narrow lane buys tucked elbows by sliding the hands to the groin.
                    float upDot = Vector3.Dot(radial / Mathf.Max(distance, 1e-4f), up);
                    residuals.Add(upDot < MinUpDot ? (MinUpDot - upDot) * 0.3f : 0f);
                    residuals.Add(fraction < RestMinFraction ? (RestMinFraction - fraction) * length * 3f : 0f);
                }
                else
                {
                    residuals.Add(isOverThigh && gap < 0f ? gap * 2f : 0f);
                    bool isNearGroin = fraction < GroinFraction && distance < SurfaceAt(profile, GroinFraction) + 0.05f;
                    residuals.Add(isNearGroin ? (GroinFraction - fraction) * length : 0f);
                }
            }
        }

        private static void AddElbowResidual(Animator animator, bool isLeft, List<float> residuals)
        {
            Transform root = animator.transform;
            float outward = isLeft ? -1f : 1f;
            float shoulder = root.InverseTransformPoint(Bone(animator, isLeft, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm)).x;
            float elbow = root.InverseTransformPoint(Bone(animator, isLeft, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm)).x;
            float outside = (elbow - shoulder) * outward;
            residuals.Add(outside < MinElbowOutside ? (MinElbowOutside - outside) * ElbowWeight : 0f);
        }

        // Pulling the elbows in for the lane brings the hands together; they must not pass through each other.
        private static void AddHandGapResidual(Animator animator, List<float> residuals)
        {
            float closest = float.MaxValue;
            foreach (HumanBodyBones left in s_leftHandJoints)
            {
                Transform leftBone = animator.GetBoneTransform(left);
                if (leftBone == null)
                {
                    continue;
                }

                foreach (HumanBodyBones right in s_rightHandJoints)
                {
                    Transform rightBone = animator.GetBoneTransform(right);
                    if (rightBone != null)
                    {
                        closest = Mathf.Min(closest, Vector3.Distance(leftBone.position, rightBone.position));
                    }
                }
            }

            residuals.Add(closest < MinHandGap ? (MinHandGap - closest) * HandGapWeight : 0f);
        }

        private static void AddLaneResiduals(Animator animator, bool isLeft, List<float> residuals)
        {
            Transform root = animator.transform;
            Vector3 shoulder = Bone(animator, isLeft, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
            Vector3 elbow = Bone(animator, isLeft, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm);
            Vector3 wrist = Bone(animator, isLeft, HumanBodyBones.LeftHand, HumanBodyBones.RightHand);
            Vector3[] points = { (shoulder + elbow) * 0.5f, elbow, (elbow + wrist) * 0.5f, wrist };
            for (int q = 0; q < points.Length; q++)
            {
                float sideways = Mathf.Abs(root.InverseTransformPoint(points[q]).x);
                float excess = sideways + s_armRadius[q] - s_laneHalfWidth;
                residuals.Add(excess > 0f ? excess * LaneWeight : 0f);
            }
        }

        private static Vector3 Bone(Animator animator, bool isLeft, HumanBodyBones left, HumanBodyBones right)
        {
            Transform bone = animator.GetBoneTransform(isLeft ? left : right);
            return bone != null ? bone.position : animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
        }

        // The first 20% of the thigh is the groin and belly crease, so the surface there is taken from the 20% sample.
        private static float SurfaceAt(float[] profile, float fraction)
        {
            float clamped = Mathf.Clamp(fraction, GroinFraction, 1f) * (ProfileSamples - 1);
            int index = Mathf.Min(ProfileSamples - 2, Mathf.FloorToInt(clamped));
            return Mathf.Lerp(profile[index], profile[index + 1], clamped - index);
        }

        private static double SquaredSum(float[] values)
        {
            double sum = 0;
            foreach (float value in values)
            {
                sum += value * value;
            }

            return sum;
        }

        private static float[] SolveDampedNormalEquations(float[][] jacobian, float[] residual, double lambda)
        {
            int n = jacobian.Length;
            double[,] matrix = new double[n, n + 1];
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                {
                    double sum = 0;
                    for (int i = 0; i < residual.Length; i++)
                    {
                        sum += jacobian[a][i] * jacobian[b][i];
                    }

                    matrix[a, b] = a == b ? sum * (1 + lambda) + 1e-6 : sum;
                }

                double gradient = 0;
                for (int i = 0; i < residual.Length; i++)
                {
                    gradient += jacobian[a][i] * residual[i];
                }

                matrix[a, n] = -gradient;
            }

            for (int column = 0; column < n; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < n; row++)
                {
                    if (System.Math.Abs(matrix[row, column]) > System.Math.Abs(matrix[pivot, column]))
                    {
                        pivot = row;
                    }
                }

                for (int k = 0; k <= n; k++)
                {
                    (matrix[column, k], matrix[pivot, k]) = (matrix[pivot, k], matrix[column, k]);
                }

                for (int row = 0; row < n; row++)
                {
                    if (row == column)
                    {
                        continue;
                    }

                    double factor = matrix[row, column] / matrix[column, column];
                    for (int k = column; k <= n; k++)
                    {
                        matrix[row, k] -= factor * matrix[column, k];
                    }
                }
            }

            float[] solution = new float[n];
            for (int j = 0; j < n; j++)
            {
                solution[j] = (float)(matrix[j, n] / matrix[j, j]);
            }

            return solution;
        }
    }
}
