using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Builds the passenger Animator Controller from the CharacterCustomizer face controller:
    /// one base-layer state per clip in the body clip folder, a head-only sleep layer, and the untouched face layers.
    /// Run it again after adding clips to the folder.
    /// </summary>
    public static class PassengerAnimatorBuilder
    {
        private const string FaceControllerPath = "Assets/CharacterCustomizer/Characters/Human/Animations/CC_Face_Animator.controller";
        private const string OutputFolder = "Assets/Animations/Passengers";
        private const string BodyClipFolder = OutputFolder + "/Mixamo";
        private const string ControllerPath = OutputFolder + "/Passenger_Animator.controller";
        private const string SleepMaskPath = OutputFolder + "/Passenger_SleepHead.mask";
        private const string SleepClipPath = OutputFolder + "/Head_Asleep.anim";
        private const string SleepLayerName = "Sleep Head";
        private const string DefaultStateName = "Stand_Idle";
        private const float SleepClipLength = 4f;

        [MenuItem("Tools/Passengers/Rebuild Animator Controller")]
        public static void Rebuild()
        {
            AnimatorController controller = LoadOrCreateController();
            if (controller == null)
            {
                return;
            }

            List<AnimationClip> bodyClips = LoadBodyClips();
            RebuildBodyLayer(controller, bodyClips);
            EnsureSleepLayer(controller, LoadOrCreateSleepClip(), LoadOrCreateSleepMask());

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"Passenger animator rebuilt: {bodyClips.Count} body states in {ControllerPath}.", controller);
        }

        private static AnimatorController LoadOrCreateController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                return controller;
            }

            // Copying keeps the FACS and Visemes layers and their parameters that FaceController drives.
            if (!AssetDatabase.CopyAsset(FaceControllerPath, ControllerPath))
            {
                Debug.LogError($"Cannot copy {FaceControllerPath} to {ControllerPath}.");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        private static List<AnimationClip> LoadBodyClips()
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { BodyClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        clips.Add(clip);
                    }
                }
            }

            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips;
        }

        private static void RebuildBodyLayer(AnimatorController controller, List<AnimationClip> clips)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                stateMachine.RemoveState(child.state);
            }

            AnimatorState defaultState = null;
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                Vector3 position = new Vector3(300f + (i / 12) * 260f, (i % 12) * 60f, 0f);
                AnimatorState state = stateMachine.AddState(clip.name, position);
                state.motion = clip;
                if (clip.name == DefaultStateName || defaultState == null)
                {
                    defaultState = state;
                }
            }

            stateMachine.defaultState = defaultState;
        }

        private static void EnsureSleepLayer(AnimatorController controller, AnimationClip sleepClip, AvatarMask mask)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == SleepLayerName)
                {
                    layers[i].avatarMask = mask;
                    controller.layers = layers;
                    return;
                }
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = SleepLayerName,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            AnimatorState state = stateMachine.AddState("Head_Asleep", new Vector3(300f, 0f, 0f));
            state.motion = sleepClip;
            stateMachine.defaultState = state;

            AnimatorControllerLayer sleepLayer = new AnimatorControllerLayer
            {
                name = SleepLayerName,
                stateMachine = stateMachine,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f
            };

            // Right after the body layer, so the additive face layers still apply on top of the dropped head.
            List<AnimatorControllerLayer> ordered = new List<AnimatorControllerLayer>(layers);
            ordered.Insert(1, sleepLayer);
            controller.layers = ordered.ToArray();
        }

        private static AvatarMask LoadOrCreateSleepMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(SleepMaskPath);
            if (mask != null)
            {
                return mask;
            }

            mask = new AvatarMask();
            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Head);
            }

            AssetDatabase.CreateAsset(mask, SleepMaskPath);
            return mask;
        }

        private static AnimationClip LoadOrCreateSleepClip()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SleepClipPath);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip { name = "Head_Asleep" };
            // Humanoid muscles: negative Nod is down. Slow breathing sway keeps the head from looking frozen.
            SetMuscleCurve(clip, "Neck Nod Down-Up", -0.55f, 0.04f);
            SetMuscleCurve(clip, "Head Nod Down-Up", -0.6f, 0.06f);
            SetMuscleCurve(clip, "Neck Tilt Left-Right", 0.25f, 0f);
            SetMuscleCurve(clip, "Head Tilt Left-Right", 0.2f, 0.02f);
            SetMuscleCurve(clip, "Neck Turn Left-Right", 0f, 0f);
            SetMuscleCurve(clip, "Head Turn Left-Right", 0.1f, 0f);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, SleepClipPath);
            return clip;
        }

        private static void SetMuscleCurve(AnimationClip clip, string muscle, float value, float breathAmplitude)
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(SleepClipLength * 0.5f, value - breathAmplitude),
                new Keyframe(SleepClipLength, value));
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
