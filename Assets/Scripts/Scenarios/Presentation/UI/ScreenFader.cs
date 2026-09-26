using System.Collections;
using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// A full-screen black layer behind the scenario panels: the world fades out and in while dialogue text stays readable.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;

        public bool IsVisible => _group.alpha > 0f;

        private void Awake()
        {
            SetAlpha(0f);
        }

        public IEnumerator Fade(float targetAlpha, float seconds)
        {
            float startAlpha = _group.alpha;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        public void SetAlpha(float alpha)
        {
            _group.alpha = alpha;
            _group.blocksRaycasts = false;
        }
    }
}
