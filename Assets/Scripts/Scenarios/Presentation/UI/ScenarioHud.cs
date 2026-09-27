using System;
using System.Collections.Generic;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Scenario title, both scales with change popups, the decision timer and an optional segmented meter for one
    /// scenario variable (e.g. panic in the wagon) that pulses when it grows.
    /// </summary>
    public sealed class ScenarioHud : MonoBehaviour
    {
        private const float DeltaVisibleSeconds = 2f;

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Slider _loyalty;
        [SerializeField] private Slider _safety;
        [SerializeField] private TMP_Text _loyaltyDelta;
        [SerializeField] private TMP_Text _safetyDelta;
        [SerializeField] private GameObject _timerRoot;
        [SerializeField] private Image _timerFill;
        [SerializeField] private TMP_Text _timerSeconds;
        [Tooltip("Optional: the radio, a world target the player carries; shown when the scenario offers it.")]
        [SerializeField] private Button _radioButton;
        [SerializeField] private Color _gainColor = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color _lossColor = new Color(0.9f, 0.3f, 0.3f);

        [Header("Meter")]
        [SerializeField] private GameObject _meterRoot;
        [SerializeField] private TMP_Text _meterLabel;
        [Tooltip("As many segments as the longest meter; unused ones are hidden.")]
        [SerializeField] private Image[] _meterSegments = new Image[0];
        [SerializeField] private Color _meterEmptyColor = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Colour of the filled segments by how full the meter is: first for low, last for full.")]
        [SerializeField] private Color[] _meterLevelColors =
        {
            new Color(0.98f, 0.82f, 0.25f), new Color(0.98f, 0.55f, 0.15f), new Color(0.9f, 0.25f, 0.2f)
        };
        [SerializeField, Range(0.05f, 2f)] private float _meterPulseSeconds = 0.5f;
        [SerializeField, Range(1f, 2f)] private float _meterPulseScale = 1.4f;

        private int _shownSeconds = -1;
        private float _hideDeltaAt;
        private int _meterSteps;
        private int _meterValue;
        private float _meterPulseLeft;

        public event Action RadioRequested;

        private void Awake()
        {
            if (_radioButton != null)
            {
                _radioButton.onClick.AddListener(OnRadioClicked);
            }

            SetRadioVisible(false);
            _loyalty.minValue = ScenarioKeys.ScaleMin;
            _loyalty.maxValue = ScenarioKeys.ScaleMax;
            _safety.minValue = ScenarioKeys.ScaleMin;
            _safety.maxValue = ScenarioKeys.ScaleMax;
            _root.SetActive(false);
            HideMeter();
        }

        private void Update()
        {
            if (_meterPulseLeft > 0f)
            {
                _meterPulseLeft = Mathf.Max(0f, _meterPulseLeft - Time.unscaledDeltaTime);
                float scale = Mathf.Lerp(1f, _meterPulseScale, Mathf.Sin(_meterPulseLeft / _meterPulseSeconds * Mathf.PI));
                for (int i = 0; i < _meterSteps; i++)
                {
                    _meterSegments[i].rectTransform.localScale = i < _meterValue ? new Vector3(scale, scale, 1f) : Vector3.one;
                }
            }

            if (_hideDeltaAt > 0f && Time.time >= _hideDeltaAt)
            {
                _hideDeltaAt = 0f;
                _loyaltyDelta.gameObject.SetActive(false);
                _safetyDelta.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_radioButton != null)
            {
                _radioButton.onClick.RemoveListener(OnRadioClicked);
            }
        }

        public void SetRadioVisible(bool isVisible)
        {
            if (_radioButton != null)
            {
                _radioButton.gameObject.SetActive(isVisible);
            }
        }

        public void Show(string title)
        {
            _root.SetActive(true);
            _title.text = title;
            _loyaltyDelta.gameObject.SetActive(false);
            _safetyDelta.gameObject.SetActive(false);
            ShowTimer(0f, 0f);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        public void SetScales(int loyalty, int safety)
        {
            _loyalty.value = loyalty;
            _safety.value = safety;
        }

        public void ShowDeltas(IReadOnlyList<AppliedEffect> effects)
        {
            int loyalty = 0;
            int safety = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Key == ScenarioKeys.Loyalty)
                {
                    loyalty += effects[i].Delta;
                }
                else if (effects[i].Key == ScenarioKeys.Safety)
                {
                    safety += effects[i].Delta;
                }
            }

            ShowDelta(_loyaltyDelta, loyalty);
            ShowDelta(_safetyDelta, safety);
            _hideDeltaAt = Time.time + DeltaVisibleSeconds;
        }

        /// <summary>Shows the meter with <paramref name="steps"/> segments, all empty.</summary>
        public void ShowMeter(string label, int steps)
        {
            if (_meterRoot == null)
            {
                return;
            }

            _meterRoot.SetActive(true);
            _meterLabel.text = label;
            _meterSteps = Mathf.Clamp(steps, 0, _meterSegments.Length);
            for (int i = 0; i < _meterSegments.Length; i++)
            {
                _meterSegments[i].gameObject.SetActive(i < _meterSteps);
                _meterSegments[i].rectTransform.localScale = Vector3.one;
            }

            _meterValue = 0;
            _meterPulseLeft = 0f;
            PaintMeter();
        }

        public void HideMeter()
        {
            if (_meterRoot != null)
            {
                _meterRoot.SetActive(false);
            }

            _meterSteps = 0;
        }

        /// <summary>Fills <paramref name="value"/> segments; a rise makes the filled ones pulse.</summary>
        public void SetMeter(int value)
        {
            if (_meterSteps == 0)
            {
                return;
            }

            value = Mathf.Clamp(value, 0, _meterSteps);
            if (value == _meterValue)
            {
                return;
            }

            if (value > _meterValue)
            {
                _meterPulseLeft = _meterPulseSeconds;
            }

            _meterValue = value;
            PaintMeter();
        }

        /// <summary>Called every frame; rewrites the seconds text only when the number changes.</summary>
        public void ShowTimer(float timeLeft, float timeLimit)
        {
            bool isTimed = timeLimit > 0f;
            if (_timerRoot.activeSelf != isTimed)
            {
                _timerRoot.SetActive(isTimed);
            }

            if (!isTimed)
            {
                _shownSeconds = -1;
                return;
            }

            _timerFill.fillAmount = Mathf.Clamp01(timeLeft / timeLimit);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timerSeconds.text = seconds.ToString();
            }
        }

        private void OnRadioClicked()
        {
            RadioRequested?.Invoke();
        }

        private void PaintMeter()
        {
            Color filled = _meterEmptyColor;
            if (_meterValue > 0 && _meterLevelColors.Length > 0)
            {
                int level = Mathf.CeilToInt((float)_meterValue / _meterSteps * _meterLevelColors.Length) - 1;
                filled = _meterLevelColors[Mathf.Clamp(level, 0, _meterLevelColors.Length - 1)];
            }

            for (int i = 0; i < _meterSteps; i++)
            {
                _meterSegments[i].color = i < _meterValue ? filled : _meterEmptyColor;
            }
        }

        private void ShowDelta(TMP_Text label, int delta)
        {
            bool isVisible = delta != 0;
            label.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                return;
            }

            label.text = delta > 0 ? "+" + delta : delta.ToString();
            label.color = delta > 0 ? _gainColor : _lossColor;
        }
    }
}
