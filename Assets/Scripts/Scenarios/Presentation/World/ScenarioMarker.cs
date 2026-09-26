using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>Floating marker above a character the player should interact with: spins and bobs to catch the eye.</summary>
    public sealed class ScenarioMarker : MonoBehaviour
    {
        private const float HighlightScale = 1.6f;

        [SerializeField, Range(0f, 360f)] private float _spinDegreesPerSecond = 90f;
        [SerializeField, Range(0f, 0.5f)] private float _bobAmplitude = 0.06f;
        [SerializeField, Range(0f, 10f)] private float _bobFrequency = 2.5f;

        private Vector3 _restPosition;
        private Vector3 _restScale;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restScale = transform.localScale;
        }

        /// <summary>Grows while the player looks at the owner.</summary>
        public void SetHighlighted(bool isHighlighted)
        {
            transform.localScale = _restScale * (isHighlighted ? HighlightScale : 1f);
        }

        private void Update()
        {
            transform.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            float bob = Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
            transform.localPosition = _restPosition + Vector3.up * bob;
        }
    }
}
