using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// Shows a group of scenario actors only while their wagon is active (portal culling), like PassengerSpawner does
    /// for random passengers. Lives on an always-active object so it can bring the actors back.
    /// </summary>
    public sealed class ScenarioActorGroup : MonoBehaviour
    {
        // CharacterCustomizer needs a few active frames to build the characters before they may be hidden.
        private const int BuildFrames = 5;

        [Tooltip("Wagon root whose active state the actors follow.")]
        [SerializeField] private GameObject _visibilitySource;
        [Tooltip("Child object holding the actors; must not be this object.")]
        [SerializeField] private GameObject _actorsRoot;

        private int _startFrame;

        private void Start()
        {
            _startFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (_visibilitySource == null || _actorsRoot == null || Time.frameCount - _startFrame < BuildFrames)
            {
                return;
            }

            bool isVisible = _visibilitySource.activeInHierarchy;
            if (_actorsRoot.activeSelf != isVisible)
            {
                _actorsRoot.SetActive(isVisible);
            }
        }
    }
}
