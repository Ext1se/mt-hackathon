using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Scenarios.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Scenario title, both scales with change popups, the decision timer, an optional meter for one scenario variable
    /// (e.g. panic in the wagon) drawn like the scales, and the devices the conductor carries (ticket terminal, radio)
    /// with their keys. Bars move smoothly and their labels pulse on a change.
    /// </summary>
    public sealed class ScenarioHud : MonoBehaviour
    {
        private const float DeltaVisibleSeconds = 2f;
        private const string TerminalActionName = "Walk/Terminal";
        private const string RadioActionName = "Walk/Radio";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Slider _loyalty;
        [SerializeField] private Slider _safety;
        [SerializeField] private TMP_Text _loyaltyLabel;
        [SerializeField] private TMP_Text _safetyLabel;
        [SerializeField] private TMP_Text _loyaltyDelta;
        [SerializeField] private TMP_Text _safetyDelta;
        [SerializeField] private GameObject _timerRoot;
        [SerializeField] private Image _timerFill;
        [SerializeField] private TMP_Text _timerSeconds;
        [Header("Devices")]
        [Tooltip("Walking actions with the device keys (Walk/Terminal, Walk/Radio).")]
        [SerializeField] private InputActionAsset _actions;
        [Tooltip("The ticket terminal (MMT); enabled while it can be opened or closed.")]
        [SerializeField] private Button _terminalButton;
        [Tooltip("The radio; enabled while the scenario offers it.")]
        [SerializeField] private Button _radioButton;
        [Tooltip("The device slots in the bottom-left corner; shown with the HUD, hidden while a dialogue is open.")]
        [SerializeField] private GameObject _devicesRoot;
        [SerializeField] private CanvasGroup _terminalSlot;
        [SerializeField] private CanvasGroup _radioSlot;
        [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.35f;
        [Tooltip("After the briefing both slots pulse until the player uses each device once.")]
        [SerializeField, Range(1f, 1.3f)] private float _pulseScale = 1.08f;
        [SerializeField, Range(0.2f, 2f)] private float _pulseSeconds = 0.6f;
        [SerializeField] private Color _gainColor = new Color(0.3f, 0.8f, 0.4f);
        [SerializeField] private Color _lossColor = new Color(0.9f, 0.3f, 0.3f);

        [Header("Bar animation")]
        [Tooltip("How long a bar takes to reach a new value.")]
        [SerializeField, Range(0.1f, 2f)] private float _barSeconds = 0.6f;
        [Tooltip("How much a bar's label and change popup swell when the value changes.")]
        [SerializeField, Range(0f, 1f)] private float _labelPunch = 0.25f;
        [SerializeField, Range(0.1f, 2f)] private float _labelPunchSeconds = 0.45f;

        [Header("Meter")]
        [SerializeField] private GameObject _meterRoot;
        [SerializeField] private TMP_Text _meterLabel;
        [SerializeField] private Slider _meterBar;
        [SerializeField] private Image _meterFill;
        [Tooltip("Colour of the bar by how full the meter is: first for low, last for full.")]
        [SerializeField] private Color[] _meterLevelColors =
        {
            new Color(0.98f, 0.82f, 0.25f), new Color(0.98f, 0.55f, 0.15f), new Color(0.9f, 0.25f, 0.2f)
        };

        private int _shownSeconds = -1;
        private float _hideDeltaAt;
        private int _meterSteps;
        private int _meterValue;
        private bool _isShown;
        private bool _isUiOpen;
        private bool _hasScales;
        private InputAction _terminalAction;
        private InputAction _radioAction;
        private Tween _terminalPulse;
        private Tween _radioPulse;

        /// <summary>The terminal button or key was used while the terminal can be opened or closed.</summary>
        public event Action TerminalRequested;

        public event Action RadioRequested;

        private void Awake()
        {
            if (_actions != null)
            {
                _terminalAction = _actions.FindAction(TerminalActionName, false);
                _radioAction = _actions.FindAction(RadioActionName, false);
            }

            _terminalButton.onClick.AddListener(OnTerminalClicked);
            _radioButton.onClick.AddListener(OnRadioClicked);
            SetDevices(false, false);
            _loyalty.minValue = ScenarioKeys.ScaleMin;
            _loyalty.maxValue = ScenarioKeys.ScaleMax;
            _safety.minValue = ScenarioKeys.ScaleMin;
            _safety.maxValue = ScenarioKeys.ScaleMax;
            _root.SetActive(false);
            RefreshDevicesShown();
            HideMeter();
        }

        private void Update()
        {
            if (_root.activeSelf)
            {
                if (_terminalAction != null && _terminalAction.WasPressedThisFrame() && _terminalButton.interactable)
                {
                    OnTerminalClicked();
                }
                else if (_radioAction != null && _radioAction.WasPressedThisFrame() && _radioButton.interactable)
                {
                    OnRadioClicked();
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
            _terminalButton.onClick.RemoveListener(OnTerminalClicked);
            _radioButton.onClick.RemoveListener(OnRadioClicked);
            StopPulses();
            KillBarTweens();
        }

        /// <summary>Both devices stay on screen for the whole scenario; unavailable ones are dimmed and ignore their key.</summary>
        public void SetDevices(bool isTerminalAvailable, bool isRadioAvailable)
        {
            _terminalButton.interactable = isTerminalAvailable;
            _radioButton.interactable = isRadioAvailable;
            SetAlpha(_terminalSlot, isTerminalAvailable);
            SetAlpha(_radioSlot, isRadioAvailable);
        }

        /// <summary>Wired from the runner: the device slots step aside while a dialogue or a panel is open.</summary>
        public void SetUiOpen(bool isOpen)
        {
            _isUiOpen = isOpen;
            RefreshDevicesShown();
        }

        /// <summary>Draws the player's eye to the devices: each slot pulses until its device is used.</summary>
        public void PulseDevices()
        {
            StopPulses();
            _terminalPulse = StartPulse(_terminalSlot);
            _radioPulse = StartPulse(_radioSlot);
        }

        public void Show(string title)
        {
            _root.SetActive(true);
            _isShown = true;
            _hasScales = false;
            RefreshDevicesShown();
            StopPulses();
            _title.text = title;
            _loyaltyDelta.gameObject.SetActive(false);
            _safetyDelta.gameObject.SetActive(false);
            ShowTimer(0f, 0f);
        }

        public void Hide()
        {
            _root.SetActive(false);
            _isShown = false;
            RefreshDevicesShown();
            StopPulses();
            KillBarTweens();
        }

        /// <summary>Moves both bars to the new values; the first call of a scenario sets them at once.</summary>
        public void SetScales(int loyalty, int safety)
        {
            bool isAnimated = _hasScales;
            _hasScales = true;
            MoveBar(_loyalty, loyalty, _loyaltyLabel, isAnimated);
            MoveBar(_safety, safety, _safetyLabel, isAnimated);
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

        /// <summary>Shows the meter as an empty bar with <paramref name="steps"/> steps.</summary>
        public void ShowMeter(string label, int steps)
        {
            if (_meterRoot == null)
            {
                return;
            }

            _meterRoot.SetActive(true);
            _meterLabel.text = label;
            _meterSteps = Mathf.Max(0, steps);
            _meterValue = 0;
            _meterBar.DOKill();
            _meterBar.minValue = 0f;
            _meterBar.maxValue = Mathf.Max(1, _meterSteps);
            _meterBar.value = 0f;
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

        /// <summary>Moves the meter to <paramref name="value"/>; its label pulses on any change.</summary>
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

            _meterValue = value;
            PaintMeter();
            MoveBar(_meterBar, value, _meterLabel, true);
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

        // A clicked HUD button would stay selected and answer Space or Enter meant for the dialogue.
        private static void Deselect()
        {
            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(null);
            }
        }

        private static void StopPulse(ref Tween pulse, CanvasGroup slot)
        {
            if (pulse != null)
            {
                pulse.Kill();
                pulse = null;
            }

            if (slot != null)
            {
                slot.transform.localScale = Vector3.one;
            }
        }

        private void OnTerminalClicked()
        {
            Deselect();
            StopPulse(ref _terminalPulse, _terminalSlot);
            TerminalRequested?.Invoke();
        }

        private void OnRadioClicked()
        {
            Deselect();
            StopPulse(ref _radioPulse, _radioSlot);
            RadioRequested?.Invoke();
        }

        private Tween StartPulse(CanvasGroup slot)
        {
            if (slot == null)
            {
                return null;
            }

            // Unscaled: the scenario pauses time in places, the hint should not freeze with it.
            return slot.transform.DOScale(_pulseScale, _pulseSeconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopPulses()
        {
            StopPulse(ref _terminalPulse, _terminalSlot);
            StopPulse(ref _radioPulse, _radioSlot);
        }

        private void RefreshDevicesShown()
        {
            if (_devicesRoot != null)
            {
                _devicesRoot.SetActive(_isShown && !_isUiOpen);
            }
        }

        private void SetAlpha(CanvasGroup slot, bool isAvailable)
        {
            if (slot != null)
            {
                slot.alpha = isAvailable ? 1f : _unavailableAlpha;
            }
        }

        private void MoveBar(Slider bar, float value, TMP_Text label, bool isAnimated)
        {
            if (bar == null)
            {
                return;
            }

            bar.DOKill();
            if (!isAnimated || Mathf.Approximately(bar.value, value))
            {
                bar.value = value;
                return;
            }

            DOTween.To(() => bar.value, current => bar.value = current, value, _barSeconds).SetEase(Ease.OutCubic)
                .SetUpdate(true).SetTarget(bar);
            Punch(label);
        }

        private void Punch(TMP_Text label)
        {
            if (label == null || _labelPunch <= 0f)
            {
                return;
            }

            Transform target = label.transform;
            target.DOKill(true);
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * _labelPunch, _labelPunchSeconds, 6, 0.6f).SetUpdate(true);
        }

        private void KillBarTweens()
        {
            _loyalty.DOKill();
            _safety.DOKill();
            if (_meterBar != null)
            {
                _meterBar.DOKill();
            }
        }

        private void PaintMeter()
        {
            if (_meterFill == null || _meterLevelColors.Length == 0)
            {
                return;
            }

            int level = _meterValue == 0 ? 0 : Mathf.CeilToInt((float)_meterValue / _meterSteps * _meterLevelColors.Length) - 1;
            _meterFill.color = _meterLevelColors[Mathf.Clamp(level, 0, _meterLevelColors.Length - 1)];
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
            Punch(label);
        }
    }
}
