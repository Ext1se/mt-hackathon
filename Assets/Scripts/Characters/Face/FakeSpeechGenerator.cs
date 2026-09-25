using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Characters.Face
{
    /// <summary>
    /// Produces plausible viseme weights without audio: phrases of random visemes separated by short pauses.
    /// </summary>
    [Serializable]
    public class FakeSpeechGenerator
    {
        private static readonly Viseme[] s_vowels = { Viseme.Aa, Viseme.E, Viseme.Ih, Viseme.Oh, Viseme.Ou };
        private static readonly Viseme[] s_consonants =
        {
            Viseme.Pp, Viseme.Ff, Viseme.Th, Viseme.Dd, Viseme.Kk, Viseme.Ch, Viseme.Ss, Viseme.Nn, Viseme.Rr
        };

        [Header("Rhythm")]
        [Tooltip("How long one viseme is held, seconds.")]
        [SerializeField, Min(0.02f)] private float _stepMin = 0.07f;
        [SerializeField, Min(0.02f)] private float _stepMax = 0.14f;
        [SerializeField, Min(0.1f)] private float _phraseMin = 0.8f;
        [SerializeField, Min(0.1f)] private float _phraseMax = 2.5f;
        [SerializeField, Min(0f)] private float _pauseMin = 0.15f;
        [SerializeField, Min(0f)] private float _pauseMax = 0.4f;

        [Header("Shape")]
        [SerializeField, Range(0f, 1f)] private float _vowelChance = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _vowelAmplitudeMin = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _vowelAmplitudeMax = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _consonantAmplitudeMin = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _consonantAmplitudeMax = 1f;
        [Tooltip("Time constant of the transition between visemes, seconds.")]
        [SerializeField, Range(0.01f, 0.2f)] private float _smoothingTime = 0.05f;

        private bool _wasTalking;
        private float _stepLeft;
        private float _phraseLeft;
        private float _pauseLeft;
        private int _currentViseme = -1;
        private float _currentAmplitude;

        /// <summary>
        /// Advances the generator and blends <paramref name="weights"/> (indexed by <see cref="Viseme"/>) towards the current viseme.
        /// </summary>
        public void Tick(float deltaTime, bool isTalking, float[] weights)
        {
            if (isTalking && !_wasTalking)
            {
                _phraseLeft = Random.Range(_phraseMin, _phraseMax);
                _pauseLeft = 0f;
                _stepLeft = 0f;
            }
            _wasTalking = isTalking;

            if (!isTalking)
            {
                _currentViseme = -1;
            }
            else if (_pauseLeft > 0f)
            {
                _pauseLeft -= deltaTime;
                _currentViseme = -1;
                if (_pauseLeft <= 0f)
                {
                    _phraseLeft = Random.Range(_phraseMin, _phraseMax);
                    _stepLeft = 0f;
                }
            }
            else
            {
                _phraseLeft -= deltaTime;
                _stepLeft -= deltaTime;
                if (_phraseLeft <= 0f)
                {
                    _pauseLeft = Random.Range(_pauseMin, _pauseMax);
                    _currentViseme = -1;
                }
                else if (_stepLeft <= 0f)
                {
                    PickNextViseme();
                }
            }

            float blend = 1f - Mathf.Exp(-deltaTime / _smoothingTime);
            for (int i = 0; i < weights.Length; i++)
            {
                float target = i == _currentViseme ? _currentAmplitude : 0f;
                weights[i] += (target - weights[i]) * blend;
            }
        }

        private void PickNextViseme()
        {
            bool isVowel = Random.value < _vowelChance;
            Viseme[] pool = isVowel ? s_vowels : s_consonants;

            int next = (int)pool[Random.Range(0, pool.Length)];
            if (next == _currentViseme)
            {
                next = (int)pool[Random.Range(0, pool.Length)];
            }

            _currentViseme = next;
            _currentAmplitude = isVowel
                ? Random.Range(_vowelAmplitudeMin, _vowelAmplitudeMax)
                : Random.Range(_consonantAmplitudeMin, _consonantAmplitudeMax);
            _stepLeft = Random.Range(_stepMin, _stepMax);
        }
    }
}
