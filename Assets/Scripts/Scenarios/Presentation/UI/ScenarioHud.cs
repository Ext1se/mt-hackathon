using System;
using System.Collections.Generic;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Scenario title, both scales with change popups, and the decision timer.</summary>
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

        private int _shownSeconds = -1;
        private float _hideDeltaAt;

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
        }

        private void Update()
        {
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
