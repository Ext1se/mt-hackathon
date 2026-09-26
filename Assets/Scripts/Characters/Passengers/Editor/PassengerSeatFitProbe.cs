using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Measures how a sitting clip fits a seat: samples the clip on a character, bakes its skinned meshes and reports,
    /// per body part, vertices inside the seat mesh and vertices closer to it than a clearance margin
    /// (the margin stands in for clothes, which are not present on the edit-mode character).
    /// </summary>
    public static class PassengerSeatFitProbe
    {
        /// <summary>Points per hand in a hand reference: wrist, palm centre, middle finger tip.</summary>
        public const int HandPointCount = 3;

        private const int ProbeLayer = 31;
        private const float RayLength = 2f;

        private static readonly Vector3[] s_directions =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };

        private static readonly HumanBodyBones[] s_legJoints =
        {
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
        };

        private static readonly HumanBodyBones[] s_parts =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
            HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };

        /// <summary>
        /// Samples <paramref name="clip"/> <paramref name="samples"/> times on <paramref name="character"/> as it stands
        /// in the scene and returns a text report. Only LOD0 skinned meshes are baked; every
        /// <paramref name="vertexStep"/>-th vertex is tested.
        /// </summary>
        public static string Evaluate(GameObject character, AnimationClip clip, MeshCollider seat, int samples = 12,
            int vertexStep = 2, float clearance = 0.015f)
        {
            return Measure(character, clip, seat, samples, vertexStep, clearance).Format();
        }

        /// <summary>
        /// Same measurement as <see cref="Evaluate"/>, as numbers for automatic fitting. With
        /// <paramref name="handReference"/> from <see cref="SampleHandReference"/> (same sample count) it also reports how far
        /// the hands drift from where the reference clip keeps them relative to the thighs.
        /// </summary>
        public static SeatFitResult Measure(GameObject character, AnimationClip clip, MeshCollider seat, int samples = 12,
            int vertexStep = 2, float clearance = 0.015f, Vector3[] handReference = null)
        {
            Animator animator = character.GetComponent<Animator>();
            List<SkinnedMeshRenderer> renderers = CollectRenderers(character);
            int partCount = s_parts.Length;
            int[] insideCount = new int[partCount];
            float[] insideDepth = new float[partCount];
            int[] nearCount = new int[partCount];
            float[] worstInsideTime = new float[partCount];
            float minHipGap = float.MaxValue;
            float insideDepthSum = 0f;
            float minFootHeight = float.MaxValue;
            float minBackGap = float.MaxValue;
            float floor = character.transform.position.y;
            Vector3 backward = -character.transform.forward;

            int originalLayer = seat.gameObject.layer;
            bool originalBackfaces = Physics.queriesHitBackfaces;
            seat.gameObject.layer = ProbeLayer;
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();

            Mesh baked = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            float maxLegSideways = 0f;
            float minKneeGap = float.MaxValue;
            float minAnkleGap = float.MaxValue;
            float handDriftSum = 0f;
            float maxHandDrift = 0f;
            try
            {
                AnimationMode.StartAnimationMode();
                for (int s = 0; s < samples; s++)
                {
                    float time = clip.length * s / samples;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(character, clip, time);
                    AnimationMode.EndSampling();

                    Segment[] segments = BuildSegments(animator);
                    foreach (HumanBodyBones legJoint in s_legJoints)
                    {
                        float sideways = Mathf.Abs(character.transform.InverseTransformPoint(animator.GetBoneTransform(legJoint).position).x);
                        maxLegSideways = Mathf.Max(maxLegSideways, sideways);
                    }

                    minKneeGap = Mathf.Min(minKneeGap, SidewaysGap(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg));
                    minAnkleGap = Mathf.Min(minAnkleGap, SidewaysGap(animator, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot));

                    if (handReference != null)
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            Transform thigh = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                            for (int point = 0; point < HandPointCount; point++)
                            {
                                Vector3 target = thigh.TransformPoint(handReference[(s * 2 + side) * HandPointCount + point]);
                                float drift = Vector3.Distance(GetHandPoint(animator, side, point), target);
                                handDriftSum += drift;
                                maxHandDrift = Mathf.Max(maxHandDrift, drift);
                            }
                        }
                    }

                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        renderer.BakeMesh(baked, true);
                        baked.GetVertices(vertices);
                        Matrix4x4 toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                        for (int v = 0; v < vertices.Count; v += vertexStep)
                        {
                            Vector3 point = toWorld.MultiplyPoint3x4(vertices[v]);
                            int part = NearestPart(segments, point);
                            HumanBodyBones bone = s_parts[part];
                            bool isInside = TryGetInsideDepth(point, out float depth);
                            if (isInside)
                            {
                                insideCount[part]++;
                                insideDepthSum += depth;
                                if (depth > insideDepth[part])
                                {
                                    insideDepth[part] = depth;
                                    worstInsideTime[part] = time;
                                }
                            }
                            else if (GetClearance(point) < clearance)
                            {
                                nearCount[part]++;
                            }

                            if (s != 0 || isInside)
                            {
                                continue;
                            }

                            if (bone == HumanBodyBones.Hips
                                && Physics.Raycast(point, Vector3.down, out RaycastHit below, RayLength, 1 << ProbeLayer))
                            {
                                minHipGap = Mathf.Min(minHipGap, below.distance);
                            }
                            else if (bone == HumanBodyBones.LeftFoot || bone == HumanBodyBones.RightFoot)
                            {
                                minFootHeight = Mathf.Min(minFootHeight, point.y - floor);
                            }
                            else if ((bone == HumanBodyBones.Chest || bone == HumanBodyBones.UpperChest)
                                && Physics.Raycast(point, backward, out RaycastHit behind, RayLength, 1 << ProbeLayer))
                            {
                                minBackGap = Mathf.Min(minBackGap, behind.distance);
                            }
                        }
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(baked);
                seat.gameObject.layer = originalLayer;
                Physics.queriesHitBackfaces = originalBackfaces;
            }

            return new SeatFitResult
            {
                Title = $"{clip.name} on {character.name}",
                Samples = samples,
                Parts = s_parts,
                InsideCount = insideCount,
                InsideDepth = insideDepth,
                WorstInsideTime = worstInsideTime,
                NearCount = nearCount,
                InsideDepthSum = insideDepthSum,
                HipGap = minHipGap == float.MaxValue ? -1f : minHipGap,
                FootHeight = minFootHeight == float.MaxValue ? -1f : minFootHeight,
                BackGap = minBackGap == float.MaxValue ? -1f : minBackGap,
                HandDrift = handReference != null ? handDriftSum / (samples * 2 * HandPointCount) : 0f,
                MaxHandDrift = maxHandDrift,
                LegHalfWidth = maxLegSideways,
                KneeGap = minKneeGap,
                AnkleGap = minAnkleGap
            };
        }

        /// <summary>
        /// Hand points (<see cref="HandPointCount"/> per hand) of <paramref name="clip"/> in the local space of the thigh on the
        /// same side, left then right for every
        /// sample. Taken from an unmodified clip, it tells a fitted clip where hands resting on the lap belong.
        /// </summary>
        public static Vector3[] SampleHandReference(GameObject character, AnimationClip clip, int samples)
        {
            Animator animator = character.GetComponent<Animator>();
            Vector3[] reference = new Vector3[samples * 2 * HandPointCount];
            try
            {
                AnimationMode.StartAnimationMode();
                for (int s = 0; s < samples; s++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(character, clip, clip.length * s / samples);
                    AnimationMode.EndSampling();
                    for (int side = 0; side < 2; side++)
                    {
                        Transform thigh = animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                        for (int point = 0; point < HandPointCount; point++)
                        {
                            reference[(s * 2 + side) * HandPointCount + point] =
                                thigh.InverseTransformPoint(GetHandPoint(animator, side, point));
                        }
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            return reference;
        }

        /// <summary>
        /// Bakes the current pose of <paramref name="character"/> into static meshes under <paramref name="parent"/>.
        /// Edit-mode captures do not re-skin sampled poses, a snapshot shows the real one.
        /// </summary>
        public static GameObject BakeSnapshot(GameObject character, Transform parent, string snapshotName)
        {
            GameObject snapshot = new GameObject(snapshotName);
            snapshot.transform.SetParent(parent, false);
            foreach (SkinnedMeshRenderer renderer in CollectRenderers(character))
            {
                Mesh mesh = new Mesh { name = renderer.name };
                renderer.BakeMesh(mesh, true);
                GameObject part = new GameObject(renderer.name);
                part.transform.SetParent(snapshot.transform, false);
                part.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }

            return snapshot;
        }

        private static float SidewaysGap(Animator animator, HumanBodyBones left, HumanBodyBones right)
        {
            Transform root = animator.transform;
            return root.InverseTransformPoint(animator.GetBoneTransform(right).position).x
                - root.InverseTransformPoint(animator.GetBoneTransform(left).position).x;
        }

        // Three points per hand pin both where the hand is and where the fingers point:
        // 0 wrist, 1 palm centre (halfway to the base of the middle finger), 2 middle finger tip.
        private static Vector3 GetHandPoint(Animator animator, int side, int point)
        {
            bool isLeft = side == 0;
            Transform hand = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform knuckle = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform tip = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
            switch (point)
            {
                case 0:
                    return hand.position;
                case 1:
                    return knuckle != null ? (hand.position + knuckle.position) * 0.5f : hand.position;
                default:
                    return tip != null ? tip.position : knuckle != null ? knuckle.position : hand.position;
            }
        }

        /// <summary>Adds a constant to every key of the listed humanoid muscle (or RootT/RootQ) curves.</summary>
        public static void OffsetCurves(AnimationClip clip, string[] properties, float[] offsets)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), properties[i]);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    Debug.LogWarning($"{clip.name} has no curve '{properties[i]}'.", clip);
                    continue;
                }

                Keyframe[] keys = curve.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    keys[k].value += offsets[i];
                }

                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// Rotates the humanoid body (RootQ) of every key by <paramref name="delta"/>, given in the character's space:
        /// e.g. a negative angle around X reclines the whole body backwards around its centre of mass.
        /// </summary>
        public static void RotateBody(AnimationClip clip, Quaternion delta)
        {
            EditorCurveBinding[] bindings =
            {
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootQ.x"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootQ.y"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootQ.z"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootQ.w")
            };

            AnimationCurve[] curves = new AnimationCurve[4];
            for (int i = 0; i < 4; i++)
            {
                curves[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                if (curves[i] == null)
                {
                    Debug.LogWarning($"{clip.name} has no RootQ curves.", clip);
                    return;
                }
            }

            Keyframe[] timeline = curves[0].keys;
            Keyframe[][] rotated = { new Keyframe[timeline.Length], new Keyframe[timeline.Length], new Keyframe[timeline.Length],
                new Keyframe[timeline.Length] };
            for (int k = 0; k < timeline.Length; k++)
            {
                float time = timeline[k].time;
                Quaternion body = new Quaternion(curves[0].Evaluate(time), curves[1].Evaluate(time), curves[2].Evaluate(time),
                    curves[3].Evaluate(time));
                Quaternion result = delta * body;
                rotated[0][k] = new Keyframe(time, result.x);
                rotated[1][k] = new Keyframe(time, result.y);
                rotated[2][k] = new Keyframe(time, result.z);
                rotated[3][k] = new Keyframe(time, result.w);
            }

            for (int i = 0; i < 4; i++)
            {
                AnimationCurve curve = new AnimationCurve(rotated[i]);
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(clip, bindings[i], curve);
            }

            EditorUtility.SetDirty(clip);
        }

        private static List<SkinnedMeshRenderer> CollectRenderers(GameObject character)
        {
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string rendererName = renderer.name;
                bool isOtherLod = rendererName.Contains("_LOD") && !rendererName.Contains("_LOD0");
                if (!renderer.enabled || isOtherLod || rendererName.Contains("Teeth") || rendererName.Contains("Shadow"))
                {
                    continue;
                }

                renderers.Add(renderer);
            }

            return renderers;
        }

        private static bool TryGetInsideDepth(Vector3 point, out float depth)
        {
            // Inside a closed shell every ray leaves through a back face; tolerate a few holes in game meshes.
            int backHits = 0;
            depth = float.MaxValue;
            for (int i = 0; i < s_directions.Length; i++)
            {
                if (Physics.Raycast(point, s_directions[i], out RaycastHit hit, RayLength, 1 << ProbeLayer)
                    && Vector3.Dot(hit.normal, s_directions[i]) > 0f)
                {
                    backHits++;
                    depth = Mathf.Min(depth, hit.distance);
                }
            }

            return backHits >= 4;
        }

        private static float GetClearance(Vector3 point)
        {
            float clearance = float.MaxValue;
            for (int i = 0; i < s_directions.Length; i++)
            {
                if (Physics.Raycast(point, s_directions[i], out RaycastHit hit, RayLength, 1 << ProbeLayer)
                    && Vector3.Dot(hit.normal, s_directions[i]) < 0f)
                {
                    clearance = Mathf.Min(clearance, hit.distance);
                }
            }

            return clearance;
        }

        private struct Segment
        {
            public Vector3 From;
            public Vector3 To;
        }

        private static Segment[] BuildSegments(Animator animator)
        {
            Segment[] segments = new Segment[s_parts.Length];
            for (int i = 0; i < s_parts.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(s_parts[i]);
                Transform child = bone != null ? FirstHumanChild(animator, s_parts[i]) : null;
                Vector3 from = bone != null ? bone.position : new Vector3(1e6f, 1e6f, 1e6f);
                Vector3 to = child != null ? child.position : from;
                if (child == null && bone != null && bone.childCount > 0)
                {
                    to = bone.GetChild(0).position;
                }

                segments[i] = new Segment { From = from, To = to };
            }

            return segments;
        }

        private static Transform FirstHumanChild(Animator animator, HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Hips: return animator.GetBoneTransform(HumanBodyBones.Spine);
                case HumanBodyBones.Spine: return animator.GetBoneTransform(HumanBodyBones.Chest);
                case HumanBodyBones.Chest: return animator.GetBoneTransform(HumanBodyBones.UpperChest);
                case HumanBodyBones.UpperChest: return animator.GetBoneTransform(HumanBodyBones.Neck);
                case HumanBodyBones.Neck: return animator.GetBoneTransform(HumanBodyBones.Head);
                case HumanBodyBones.LeftShoulder: return animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                case HumanBodyBones.LeftUpperArm: return animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                case HumanBodyBones.LeftLowerArm: return animator.GetBoneTransform(HumanBodyBones.LeftHand);
                case HumanBodyBones.RightShoulder: return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                case HumanBodyBones.RightUpperArm: return animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                case HumanBodyBones.RightLowerArm: return animator.GetBoneTransform(HumanBodyBones.RightHand);
                case HumanBodyBones.LeftUpperLeg: return animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                case HumanBodyBones.LeftLowerLeg: return animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                case HumanBodyBones.LeftFoot: return animator.GetBoneTransform(HumanBodyBones.LeftToes);
                case HumanBodyBones.RightUpperLeg: return animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                case HumanBodyBones.RightLowerLeg: return animator.GetBoneTransform(HumanBodyBones.RightFoot);
                case HumanBodyBones.RightFoot: return animator.GetBoneTransform(HumanBodyBones.RightToes);
                default: return null;
            }
        }

        private static int NearestPart(Segment[] segments, Vector3 point)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < segments.Length; i++)
            {
                Vector3 axis = segments[i].To - segments[i].From;
                float t = axis.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(point - segments[i].From, axis) / axis.sqrMagnitude) : 0f;
                float distance = (segments[i].From + axis * t - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }
    }
}
