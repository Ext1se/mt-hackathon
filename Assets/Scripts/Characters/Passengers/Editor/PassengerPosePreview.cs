using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Poses a passenger on its spot in Edit Mode by sampling a clip of the pose through its Animator, so placed
    /// passengers are seen sitting or standing without Play Mode. The sampled bone transforms stay in the scene;
    /// Play Mode drives them again from scratch.
    /// </summary>
    public static class PassengerPosePreview
    {
        private const int BodyLayer = 0;
        private const string UndoName = "Preview Passenger Pose";

        /// <summary>Moves <paramref name="passenger"/> onto <paramref name="spot"/> and samples a looping clip of <paramref name="pose"/>.</summary>
        public static bool Apply(Passenger passenger, PassengerSpot spot, PassengerPose pose)
        {
            if (passenger == null || spot == null)
            {
                return false;
            }

            Undo.RegisterFullObjectHierarchyUndo(passenger.gameObject, UndoName);
            passenger.transform.SetPositionAndRotation(Passenger.RootPosition(spot, pose, passenger.AnimationSet),
                spot.transform.rotation);

            if (!passenger.TryGetComponent(out Animator animator) || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"'{passenger.name}' has no Animator Controller, pose preview skipped.", passenger);
                return false;
            }

            PassengerAnimationSet set = spot.AnimationSet != null ? spot.AnimationSet : passenger.AnimationSet;
            if (set == null)
            {
                Debug.LogWarning($"'{passenger.name}' has no animation set, pose preview skipped.", passenger);
                return false;
            }

            AnimationClip clip = PickLoopingClip(set.GetClips(pose));
            if (clip == null)
            {
                Debug.LogWarning($"Animation set '{set.name}' has no looping clip for {pose}, pose preview skipped.", set);
                return false;
            }

            int stateHash = Animator.StringToHash(clip.name);
            if (!animator.HasState(BodyLayer, stateHash))
            {
                Debug.LogWarning($"Animator has no state '{clip.name}' (Tools/Passengers/Rebuild Animator Controller).", passenger);
                return false;
            }

            // Update(0) after Play writes the sampled pose into the bone transforms even outside Play Mode.
            animator.Play(stateHash, BodyLayer, Random.value);
            animator.Update(0f);

            if (passenger.TryGetComponent(out PassengerHandProp handProp))
            {
                handProp.Apply(clip);
            }

            return true;
        }

        private static AnimationClip PickLoopingClip(AnimationClip[] clips)
        {
            int candidates = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].isLooping)
                {
                    candidates++;
                }
            }

            if (candidates == 0)
            {
                return null;
            }

            int pick = Random.Range(0, candidates);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].isLooping && pick-- == 0)
                {
                    return clips[i];
                }
            }

            return null;
        }
    }
}
