using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Characters.Face
{
    /// <summary>
    /// Procedural blinking and small eye saccades that keep an idle face alive.
    /// </summary>
    [Serializable]
    public class IdleEyeMotion
    {
        private const float DoubleBlinkDelay = 0.12f;

        [Header("Blink")]
        [SerializeField, Min(0.1f)] private float _blinkIntervalMin = 2f;
        [SerializeField, Min(0.1f)] private float _blinkIntervalMax = 6f;
        [SerializeField, Range(0.02f, 0.3f)] private float _blinkCloseTime = 0.06f;
        [SerializeField, Range(0.02f, 0.3f)] private float _blinkOpenTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _doubleBlinkChance = 0.15f;

        [Header("Saccades")]
        [SerializeField, Min(0.05f)] private float _saccadeIntervalMin = 0.6f;
        [SerializeField, Min(0.05f)] private float _saccadeIntervalMax = 2.5f;
        [Tooltip("Maximum eye look weight of a random glance.")]
        [SerializeField, Range(0f, 1f)] private float _saccadeAmplitude = 0.15f;
        [Tooltip("Chance that a saccade returns the gaze to the center.")]
        [SerializeField, Range(0f, 1f)] private float _centerChance = 0.4f;
        [SerializeField, Min(1f)] private float _saccadeSpeed = 25f;

        private float _nextBlinkIn;
        private float _blinkTime = -1f;
        private bool _isSecondBlink;
        private float _nextSaccadeIn;
        private Vector2 _lookTarget;
        private Vector2 _look;

        /// <summary>Eyelid closure, 0 = open, 1 = closed.</summary>
        public float Blink { get; private set; }

        /// <summary>Gaze offset: x = right, y = up, both in look-weight units.</summary>
        public Vector2 Look => _look;

        public void Reset()
        {
            // Randomized so that characters spawned together do not blink in sync.
            _nextBlinkIn = Random.Range(0f, _blinkIntervalMax);
            _nextSaccadeIn = Random.Range(0f, _saccadeIntervalMax);
            _blinkTime = -1f;
            _isSecondBlink = false;
            _lookTarget = Vector2.zero;
            _look = Vector2.zero;
            Blink = 0f;
        }

        public void Tick(float deltaTime)
        {
            TickBlink(deltaTime);
            TickSaccade(deltaTime);
        }

        private void TickBlink(float deltaTime)
        {
            if (_blinkTime < 0f)
            {
                _nextBlinkIn -= deltaTime;
                if (_nextBlinkIn <= 0f)
                {
                    _blinkTime = 0f;
                }
                return;
            }

            _blinkTime += deltaTime;
            if (_blinkTime < _blinkCloseTime)
            {
                Blink = _blinkTime / _blinkCloseTime;
                return;
            }

            float openTime = _blinkTime - _blinkCloseTime;
            if (openTime < _blinkOpenTime)
            {
                Blink = 1f - openTime / _blinkOpenTime;
                return;
            }

            Blink = 0f;
            _blinkTime = -1f;
            if (!_isSecondBlink && Random.value < _doubleBlinkChance)
            {
                _isSecondBlink = true;
                _nextBlinkIn = DoubleBlinkDelay;
            }
            else
            {
                _isSecondBlink = false;
                _nextBlinkIn = Random.Range(_blinkIntervalMin, _blinkIntervalMax);
            }
        }

        private void TickSaccade(float deltaTime)
        {
            _nextSaccadeIn -= deltaTime;
            if (_nextSaccadeIn <= 0f)
            {
                _lookTarget = Random.value < _centerChance ? Vector2.zero : Random.insideUnitCircle * _saccadeAmplitude;
                _nextSaccadeIn = Random.Range(_saccadeIntervalMin, _saccadeIntervalMax);
            }

            _look = Vector2.Lerp(_look, _lookTarget, 1f - Mathf.Exp(-_saccadeSpeed * deltaTime));
        }
    }
}
