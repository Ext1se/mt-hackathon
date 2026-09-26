using TMPro;
using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Temporary hint line. Root must be a child object: this component keeps running to hide it.</summary>
    public sealed class HintView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _text;
        [SerializeField, Range(1f, 20f)] private float _visibleSeconds = 8f;

        private float _hideAt;

        private void Awake()
        {
            _root.SetActive(false);
        }

        private void Update()
        {
            if (_root.activeSelf && Time.time >= _hideAt)
            {
                _root.SetActive(false);
            }
        }

        public void Show(string text)
        {
            _text.text = text;
            _root.SetActive(true);
            _hideAt = Time.time + _visibleSeconds;
        }

        public void Hide()
        {
            _root.SetActive(false);
        }
    }
}
