using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Lets a scroll view inside a layout group take the height of its content up to a cap, so short lists
    /// do not leave empty space and long lists scroll.
    /// </summary>
    [RequireComponent(typeof(LayoutElement))]
    public sealed class ScrollListHeight : MonoBehaviour
    {
        [SerializeField] private LayoutElement _element;
        [SerializeField] private RectTransform _content;
        [SerializeField, Min(0f)] private float _maxHeight = 260f;

        private void Reset()
        {
            _element = GetComponent<LayoutElement>();
        }

        private void LateUpdate()
        {
            if (_element == null || _content == null)
            {
                return;
            }

            // An empty list takes no room: the content's own padding would otherwise leave a gap under a response.
            float height = HasActiveChild() ? Mathf.Min(LayoutUtility.GetPreferredHeight(_content), _maxHeight) : 0f;
            if (!Mathf.Approximately(_element.preferredHeight, height))
            {
                _element.preferredHeight = height;
            }
        }

        private bool HasActiveChild()
        {
            for (int i = 0; i < _content.childCount; i++)
            {
                if (_content.GetChild(i).gameObject.activeSelf)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
