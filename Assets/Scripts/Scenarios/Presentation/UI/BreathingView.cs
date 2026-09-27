using System;
using Game.Scenarios.Core;
using Game.Scenarios.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// The box-breathing mini-game panel: a dot runs round a square, one side per phase (left: inhale, top: hold,
    /// right: exhale, bottom: pause), the inner square fills with the breath and the count goes one to four.
    /// The player presses Space (or the left mouse button, or a touch) on the inhale, holds it through the pause, releases
    /// it on the exhale and leaves it up through the last pause (see <see cref="BoxBreathing"/>). A short countdown comes first,
    /// the result is shown for a moment, then <see cref="Finished"/> reports whether the rhythm was kept.
    /// </summary>
    public sealed class BreathingView : MonoBehaviour
    {
        // Square corners in half-sizes, from the bottom left clockwise: phase i runs from corner i to corner i + 1.
        private static readonly Vector2[] s_corners =
        {
            new Vector2(-1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, -1f)
        };

        private static readonly string[] s_counts = { "1", "2", "3", "4" };

        [Tooltip("Full-screen layer that takes all clicks while the game runs; holds the panel.")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _cycleLabel;
        [SerializeField] private TMP_Text _phaseLabel;
        [SerializeField] private TMP_Text _counter;
        [SerializeField] private TMP_Text _prompt;
        [Tooltip("The square the dot runs along; the dot moves on its edges.")]
        [SerializeField] private RectTransform _square;
        [SerializeField] private RectTransform _dot;
        [Tooltip("Inner square scaled with the breath: small when empty, full size when the lungs are full.")]
        [SerializeField] private RectTransform _lungs;
        [Tooltip("Square sides in phase order: left (inhale), top (hold), right (exhale), bottom (pause).")]
        [SerializeField] private Image[] _sides = new Image[BoxBreathing.PhaseCount];
        [Tooltip("Phase names next to the sides, in the same order.")]
        [SerializeField] private TMP_Text[] _sideLabels = new TMP_Text[BoxBreathing.PhaseCount];
        [SerializeField] private Slider _scoreBar;

        [Header("Timing")]
        [SerializeField, Range(0f, 5f)] private float _leadInSeconds = 3f;
        [SerializeField, Range(0.5f, 5f)] private float _resultSeconds = 2f;
        [SerializeField, Range(0.1f, 1f)] private float _emptyLungsScale = 0.4f;

        [Header("Colors")]
        [SerializeField] private Color _sideIdleColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color _sideActiveColor = new Color(0.98f, 0.78f, 0.22f, 1f);
        [SerializeField] private Color _labelIdleColor = new Color(0.75f, 0.78f, 0.83f, 1f);
        [SerializeField] private Color _labelActiveColor = Color.white;
        [SerializeField] private Color _neutralColor = Color.white;
        [SerializeField] private Color _matchColor = new Color(0.35f, 0.80f, 0.45f, 1f);
        [SerializeField] private Color _missColor = new Color(0.90f, 0.25f, 0.20f, 1f);

        [Header("Texts (filled by the scene builder)")]
        [Tooltip("Phase names: inhale, pause, exhale, pause.")]
        [SerializeField] private string[] _phaseNames = new string[BoxBreathing.PhaseCount];
        [Tooltip("What to do with the key in each phase, same order: press, hold, release, leave up.")]
        [SerializeField] private string[] _phasePrompts = new string[BoxBreathing.PhaseCount];
        [Tooltip("{0} is the current cycle, {1} the number of cycles.")]
        [SerializeField] private string _cycleFormat = "{0} / {1}";
        [SerializeField] private string _getReadyText = string.Empty;
        [SerializeField] private string _leadInPrompt = string.Empty;
        [SerializeField] private string _passedText = string.Empty;
        [SerializeField] private string _failedText = string.Empty;
        [Tooltip("{0} is the score in percent.")]
        [SerializeField] private string _scoreFormat = "{0}%";

        private BreathingData _data;
        private BoxBreathing _game;
        private float _leadInLeft;
        private float _resultLeft;
        private bool _passed;
        // Texts are rewritten only when what they show changes, so a running game allocates nothing per frame.
        private int _shownStep = -1;
        private int _shownCount = -1;

        /// <summary>Raised once the result has been on screen: true when the rhythm was kept.</summary>
        public event Action<bool> Finished;

        public bool IsShown => _root.activeSelf;

        private void Awake()
        {
            _root.SetActive(false);
        }

        private void Update()
        {
            if (_data == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (_leadInLeft > 0f)
            {
                _leadInLeft -= deltaTime;
                if (_leadInLeft > 0f)
                {
                    ShowLeadIn();
                }
                else
                {
                    // The game's own count starts next frame; the countdown's last digit must not hide it.
                    _shownCount = -1;
                }

                return;
            }

            if (!_game.IsDone)
            {
                bool isHeld = IsHeld();
                _game.Tick(deltaTime, isHeld);
                if (_game.IsDone)
                {
                    ShowResult();
                }
                else
                {
                    ShowGame(isHeld);
                }

                return;
            }

            _resultLeft -= deltaTime;
            if (_resultLeft <= 0f)
            {
                _data = null;
                Finished?.Invoke(_passed);
            }
        }

        public void Show(BreathingData data)
        {
            _data = data;
            _game = new BoxBreathing(data.PhaseSeconds, data.Cycles);
            _leadInLeft = _leadInSeconds;
            _resultLeft = _resultSeconds;
            _passed = false;
            _shownStep = -1;
            _shownCount = -1;
            // A button left selected by an earlier click would be pressed by Space (UI submit) on every breath.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }

            _root.SetActive(true);
            _title.text = data.Title;
            _title.gameObject.SetActive(!string.IsNullOrEmpty(data.Title));
            for (int i = 0; i < _sideLabels.Length; i++)
            {
                _sideLabels[i].text = _phaseNames[i];
            }

            _scoreBar.value = 0f;
            _cycleLabel.text = string.Format(_cycleFormat, 1, data.Cycles);
            _phaseLabel.text = _getReadyText;
            _counter.text = string.Empty;
            _prompt.text = _leadInPrompt;
            _prompt.color = _neutralColor;
            HighlightSide(-1);
            PlaceDot(0, 0f);
            SetLungs(_emptyLungsScale);
            if (_leadInLeft > 0f)
            {
                ShowLeadIn();
            }
        }

        public void Hide()
        {
            _data = null;
            _root.SetActive(false);
        }

        private static bool IsHeld()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Touchscreen touch = Touchscreen.current;
            return (keyboard != null && keyboard.spaceKey.isPressed)
                || (mouse != null && mouse.leftButton.isPressed)
                || (touch != null && touch.primaryTouch.press.isPressed);
        }

        // Shader-style smoothstep of 0..1: the breath speeds up and slows down like a real one.
        private static float Ease(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void ShowLeadIn()
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(_leadInLeft), 1, s_counts.Length);
            if (count != _shownCount)
            {
                _shownCount = count;
                _counter.text = s_counts[count - 1];
            }
        }

        private void ShowGame(bool isHeld)
        {
            int phase = (int)_game.Phase;
            int step = _game.Cycle * BoxBreathing.PhaseCount + phase;
            if (step != _shownStep)
            {
                _shownStep = step;
                _phaseLabel.text = _phaseNames[phase];
                _prompt.text = _phasePrompts[phase];
                _cycleLabel.text = string.Format(_cycleFormat, _game.Cycle + 1, _game.Cycles);
                HighlightSide(phase);
            }

            int count = _game.Count;
            if (count != _shownCount)
            {
                _shownCount = count;
                _counter.text = s_counts[count - 1];
            }

            float progress = _game.PhaseProgress;
            PlaceDot(phase, progress);
            SetLungs(LungsScale(_game.Phase, progress));
            _prompt.color = _game.IsInGrace ? _neutralColor : _game.IsMatching(isHeld) ? _matchColor : _missColor;
            _scoreBar.value = _game.Score;
        }

        private void ShowResult()
        {
            _passed = _game.Score >= _data.PassScore;
            int percent = Mathf.RoundToInt(_game.Score * 100f);
            _phaseLabel.text = _passed ? _passedText : _failedText;
            _counter.text = string.Format(_scoreFormat, percent);
            _prompt.text = string.Empty;
            _scoreBar.value = _game.Score;
            HighlightSide(-1);
            PlaceDot(0, 0f);
            SetLungs(_emptyLungsScale);
        }

        private float LungsScale(BreathingPhase phase, float progress)
        {
            switch (phase)
            {
                case BreathingPhase.Inhale:
                    return Mathf.Lerp(_emptyLungsScale, 1f, Ease(progress));
                case BreathingPhase.HoldFull:
                    return 1f;
                case BreathingPhase.Exhale:
                    return Mathf.Lerp(1f, _emptyLungsScale, Ease(progress));
                default:
                    return _emptyLungsScale;
            }
        }

        private void PlaceDot(int phase, float progress)
        {
            Vector2 half = _square.rect.size * 0.5f;
            Vector2 from = s_corners[phase];
            Vector2 to = s_corners[(phase + 1) % s_corners.Length];
            _dot.anchoredPosition = Vector2.Scale(Vector2.Lerp(from, to, progress), half);
        }

        private void SetLungs(float scale)
        {
            _lungs.localScale = new Vector3(scale, scale, 1f);
        }

        // -1 dims every side (countdown and result).
        private void HighlightSide(int phase)
        {
            for (int i = 0; i < _sides.Length; i++)
            {
                bool isActive = i == phase;
                _sides[i].color = isActive ? _sideActiveColor : _sideIdleColor;
                _sideLabels[i].color = isActive ? _labelActiveColor : _labelIdleColor;
            }
        }
    }
}
