using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// Shows what the player is looking at: the scenario object under the view ray gets a warm tint and a bigger marker,
    /// the reticle grows and a label names the action. Follows the same ray as the interaction, so what is highlighted
    /// is what the action button will use.
    /// </summary>
    public sealed class LookHighlighter : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private Camera _view;
        [SerializeField, Range(0.5f, 5f)] private float _range = 2.3f;
        [Header("Reticle")]
        [SerializeField] private Image _reticle;
        [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color _hoverColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField, Range(1f, 3f)] private float _hoverScale = 1.8f;
        [Header("Label")]
        [SerializeField] private GameObject _labelRoot;
        [SerializeField] private TMP_Text _label;
        [Tooltip("Label for a scenario starter; the words live in ScenarioUiStrings.json.")]
        [SerializeField] private string _startLabel = string.Empty;
        [Tooltip("Appended on platforms with a keyboard, e.g. ' · E'.")]
        [SerializeField] private string _keyHint = string.Empty;
        [Header("Object tint")]
        [SerializeField] private Color _tint = new Color(1.25f, 1.1f, 0.8f, 1f);

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<Renderer> _tinted = new List<Renderer>();
        private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private GameObject _current;
        private ScenarioMarker _currentMarker;
        private bool _isUiOpen;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetHover(null);
        }

        private void Update()
        {
            GameObject target = _isUiOpen ? null : FindTarget();
            if (target != _current)
            {
                SetHover(target);
            }
        }

        private void OnDisable()
        {
            SetHover(null);
        }

        /// <summary>Wired to ScenarioRunner's Ui Open Changed event: nothing is highlighted behind a dialogue.</summary>
        public void SetUiOpen(bool isOpen)
        {
            _isUiOpen = isOpen;
            if (isOpen)
            {
                SetHover(null);
            }
        }

        // The nearest object along the view ray that has a scenario component, looking through furniture like the interactor.
        private GameObject FindTarget()
        {
            if (_view == null)
            {
                return null;
            }

            int count = Physics.RaycastNonAlloc(_view.transform.position, _view.transform.forward, _hits, _range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            GameObject nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance >= nearestDistance)
                {
                    continue;
                }

                IScenarioInteractable interactable = _hits[i].collider.GetComponentInParent<IScenarioInteractable>();
                if (interactable is Component component && IsUsable(component.gameObject))
                {
                    nearest = component.gameObject;
                    nearestDistance = _hits[i].distance;
                }
            }

            return nearest;
        }

        // Only objects the scenario can use right now: the starter before it starts, targets of the current objectives.
        private bool IsUsable(GameObject candidate)
        {
            if (_runner == null)
            {
                return true;
            }

            if (candidate.TryGetComponent(out ScenarioStarter starter) && !_runner.IsRunning)
            {
                return true;
            }

            return candidate.TryGetComponent(out ScenarioInteractable interactable)
                && _runner.TryGetObjective(interactable.TargetId, out string _);
        }

        private void SetHover(GameObject target)
        {
            ClearTint();
            if (_currentMarker != null)
            {
                _currentMarker.SetHighlighted(false);
                _currentMarker = null;
            }

            _current = target;
            bool isHovering = target != null;
            if (_reticle != null)
            {
                _reticle.color = isHovering ? _hoverColor : _idleColor;
                _reticle.rectTransform.localScale = Vector3.one * (isHovering ? _hoverScale : 1f);
            }

            if (_labelRoot != null)
            {
                _labelRoot.SetActive(isHovering);
            }

            if (!isHovering)
            {
                return;
            }

            ApplyTint(target);
            _currentMarker = target.GetComponentInChildren<ScenarioMarker>(true);
            if (_currentMarker != null)
            {
                _currentMarker.SetHighlighted(true);
            }

            if (_label != null)
            {
                _label.text = LabelFor(target) + (Application.isMobilePlatform ? string.Empty : _keyHint);
            }
        }

        private string LabelFor(GameObject target)
        {
            if (target.TryGetComponent(out ScenarioInteractable interactable) && _runner != null
                && _runner.TryGetObjective(interactable.TargetId, out string objective))
            {
                return objective;
            }

            return _startLabel;
        }

        private void ApplyTint(GameObject target)
        {
            target.GetComponentsInChildren(true, _rendererBuffer);
            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                Renderer renderer = _rendererBuffer[i];
                Material material = renderer.sharedMaterial;
                if (material == null || renderer.GetComponentInParent<ScenarioMarker>() != null)
                {
                    continue;
                }

                int colorId = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : -1;
                if (colorId < 0)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetColor(colorId, material.GetColor(colorId) * _tint);
                renderer.SetPropertyBlock(_block);
                _tinted.Add(renderer);
            }
        }

        private void ClearTint()
        {
            for (int i = 0; i < _tinted.Count; i++)
            {
                if (_tinted[i] != null)
                {
                    _tinted[i].SetPropertyBlock(null);
                }
            }

            _tinted.Clear();
        }
    }
}
