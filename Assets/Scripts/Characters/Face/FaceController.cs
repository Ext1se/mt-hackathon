using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters.Face
{
    /// <summary>
    /// Drives the facial rig of a CharacterCustomizer character through the FACS and Visemes layers of CC_Face_Animator:
    /// crossfaded expressions, idle eye motion and fake speech. It must be the only writer of these Animator parameters.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FaceController : MonoBehaviour
    {
        private static readonly FaceParameter[] s_noiseParameters =
        {
            FaceParameter.BrowInnerUp, FaceParameter.BrowOuterUpLeft, FaceParameter.BrowOuterUpRight,
            FaceParameter.MouthSmileLeft, FaceParameter.MouthSmileRight, FaceParameter.MouthPressLeft, FaceParameter.MouthPressRight
        };

        [SerializeField] private Animator _animator;

        [Header("Expressions")]
        [Tooltip("Expression used on start and after ClearExpression. Empty means a neutral face.")]
        [SerializeField] private FaceExpression _defaultExpression;
        [SerializeField, Min(0f)] private float _defaultFadeTime = 0.25f;

        [Header("Idle")]
        [SerializeField] private IdleEyeMotion _eyeMotion = new IdleEyeMotion();
        [Tooltip("Amplitude of the subtle noise on brows and mouth corners.")]
        [SerializeField, Range(0f, 0.2f)] private float _idleNoiseAmplitude = 0.04f;
        [SerializeField, Range(0.05f, 2f)] private float _idleNoiseFrequency = 0.3f;

        [Header("Speech")]
        [SerializeField] private FakeSpeechGenerator _speech = new FakeSpeechGenerator();
        [Tooltip("Multiplier for the mouth part of the current expression while talking, so visemes stay readable.")]
        [SerializeField, Range(0f, 1f)] private float _mouthWeightWhileTalking = 0.5f;
        [SerializeField, Min(0.01f)] private float _talkBlendTime = 0.15f;

        private int[] _parameterHashes;
        private bool[] _hasParameter;
        private bool[] _isMouth;
        private int[] _visemeHashes;
        private bool[] _hasViseme;

        private float[] _fromWeights;
        private float[] _toWeights;
        private float[] _expressionWeights;
        private float[] _outputWeights;
        private float[] _visemeWeights;

        private FaceExpression _currentExpression;
        private float _currentIntensity = 1f;
        private float _fadeProgress = 1f;
        private float _fadeDuration;
        private float _talkBlend;
        private float _talkUntil;
        private bool _isTalkingIndefinitely;
        private float _noiseSeed;
        private bool _isReady;

        public FaceExpression CurrentExpression => _currentExpression;
        public bool IsTalking => _isTalkingIndefinitely || Time.time < _talkUntil;

        private void Awake()
        {
            if (_animator == null && !TryGetComponent(out _animator))
            {
                enabled = false;
                return;
            }

            if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"{nameof(FaceController)} on '{name}': Animator has no controller, face is disabled.", this);
                enabled = false;
                return;
            }

            BuildParameterMap();
            _eyeMotion.Reset();
            _noiseSeed = UnityEngine.Random.Range(0f, 1000f);
            SetExpression(_defaultExpression, 1f, 0f);
            _isReady = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            TickExpression(deltaTime);
            _talkBlend = Mathf.MoveTowards(_talkBlend, IsTalking ? 1f : 0f, deltaTime / _talkBlendTime);
            _speech.Tick(deltaTime, IsTalking, _visemeWeights);
            _eyeMotion.Tick(deltaTime);

            ComposeOutput(Time.time);
            WriteToAnimator();
        }

        private void OnDisable()
        {
            if (!_isReady)
            {
                return;
            }

            // Do not leave a frozen expression on the face when the controller stops.
            Array.Clear(_outputWeights, 0, _outputWeights.Length);
            Array.Clear(_visemeWeights, 0, _visemeWeights.Length);
            WriteToAnimator();
        }

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Crossfades to <paramref name="expression"/>. Null means a neutral face.
        /// A negative <paramref name="fadeTime"/> uses the expression's own fade time or the controller default.
        /// </summary>
        public void SetExpression(FaceExpression expression, float intensity = 1f, float fadeTime = -1f)
        {
            if (_expressionWeights == null)
            {
                return;
            }

            _currentExpression = expression;
            _currentIntensity = Mathf.Clamp01(intensity);

            Array.Copy(_expressionWeights, _fromWeights, _expressionWeights.Length);
            FillTarget();

            if (fadeTime < 0f)
            {
                fadeTime = expression != null && expression.FadeTime >= 0f ? expression.FadeTime : _defaultFadeTime;
            }

            _fadeDuration = fadeTime;
            _fadeProgress = fadeTime > 0f ? 0f : 1f;
            if (_fadeProgress >= 1f)
            {
                Array.Copy(_toWeights, _expressionWeights, _toWeights.Length);
            }
        }

        public void ClearExpression(float fadeTime = -1f)
        {
            SetExpression(_defaultExpression, 1f, fadeTime);
        }

        /// <summary>Talks until <see cref="StopTalking"/> is called.</summary>
        public void StartTalking()
        {
            _isTalkingIndefinitely = true;
        }

        public void StopTalking()
        {
            _isTalkingIndefinitely = false;
            _talkUntil = 0f;
        }

        /// <summary>Talks for <paramref name="duration"/> seconds, e.g. while a subtitle line is on screen.</summary>
        public void Talk(float duration)
        {
            _talkUntil = Mathf.Max(_talkUntil, Time.time + duration);
        }

        private void BuildParameterMap()
        {
            HashSet<string> animatorFloats = new HashSet<string>();
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float)
                {
                    animatorFloats.Add(parameter.name);
                }
            }

            int count = FaceRigParameters.FaceParameterCount;
            _parameterHashes = new int[count];
            _hasParameter = new bool[count];
            _isMouth = new bool[count];
            for (int i = 0; i < count; i++)
            {
                string parameterName = FaceRigParameters.GetName((FaceParameter)i);
                _parameterHashes[i] = Animator.StringToHash(parameterName);
                _hasParameter[i] = animatorFloats.Contains(parameterName);
                _isMouth[i] = FaceRigParameters.IsMouth((FaceParameter)i);
            }

            int visemeCount = FaceRigParameters.VisemeCount;
            _visemeHashes = new int[visemeCount];
            _hasViseme = new bool[visemeCount];
            for (int i = 0; i < visemeCount; i++)
            {
                string visemeName = FaceRigParameters.VisemeNames[i];
                _visemeHashes[i] = Animator.StringToHash(visemeName);
                _hasViseme[i] = animatorFloats.Contains(visemeName);
            }

            _fromWeights = new float[count];
            _toWeights = new float[count];
            _expressionWeights = new float[count];
            _outputWeights = new float[count];
            _visemeWeights = new float[visemeCount];
        }

        private void FillTarget()
        {
            if (_currentExpression != null)
            {
                _currentExpression.Fill(_toWeights, _currentIntensity);
            }
            else
            {
                Array.Clear(_toWeights, 0, _toWeights.Length);
            }
        }

        private void TickExpression(float deltaTime)
        {
            if (_fadeProgress >= 1f)
            {
#if UNITY_EDITOR
                // Lets expression assets be tuned live in the inspector during Play Mode.
                FillTarget();
                Array.Copy(_toWeights, _expressionWeights, _toWeights.Length);
#endif
                return;
            }

            _fadeProgress = Mathf.Min(1f, _fadeProgress + deltaTime / _fadeDuration);
            float t = Mathf.SmoothStep(0f, 1f, _fadeProgress);
            for (int i = 0; i < _expressionWeights.Length; i++)
            {
                _expressionWeights[i] = Mathf.Lerp(_fromWeights[i], _toWeights[i], t);
            }
        }

        private void ComposeOutput(float time)
        {
            float mouthScale = Mathf.Lerp(1f, _mouthWeightWhileTalking, _talkBlend);
            for (int i = 0; i < _outputWeights.Length; i++)
            {
                _outputWeights[i] = _isMouth[i] ? _expressionWeights[i] * mouthScale : _expressionWeights[i];
            }

            for (int i = 0; i < s_noiseParameters.Length; i++)
            {
                float noise = Mathf.PerlinNoise(_noiseSeed + i * 17.3f, time * _idleNoiseFrequency);
                _outputWeights[(int)s_noiseParameters[i]] += noise * _idleNoiseAmplitude;
            }

            ApplyEyeMotion();

            for (int i = 0; i < _outputWeights.Length; i++)
            {
                _outputWeights[i] = Mathf.Clamp01(_outputWeights[i]);
            }
        }

        private void ApplyEyeMotion()
        {
            float blink = _eyeMotion.Blink;
            int blinkLeft = (int)FaceParameter.EyeBlinkLeft;
            int blinkRight = (int)FaceParameter.EyeBlinkRight;
            _outputWeights[blinkLeft] = Mathf.Max(_outputWeights[blinkLeft], blink);
            _outputWeights[blinkRight] = Mathf.Max(_outputWeights[blinkRight], blink);
            // Wide-open eyes (surprise, fear) would otherwise fight the eyelids during a blink.
            _outputWeights[(int)FaceParameter.EyeWideLeft] *= 1f - blink;
            _outputWeights[(int)FaceParameter.EyeWideRight] *= 1f - blink;

            Vector2 look = _eyeMotion.Look;
            if (look.x >= 0f)
            {
                _outputWeights[(int)FaceParameter.EyeLookOutRight] += look.x;
                _outputWeights[(int)FaceParameter.EyeLookInLeft] += look.x;
            }
            else
            {
                _outputWeights[(int)FaceParameter.EyeLookInRight] -= look.x;
                _outputWeights[(int)FaceParameter.EyeLookOutLeft] -= look.x;
            }

            if (look.y >= 0f)
            {
                _outputWeights[(int)FaceParameter.EyeLookUpLeft] += look.y;
                _outputWeights[(int)FaceParameter.EyeLookUpRight] += look.y;
            }
            else
            {
                _outputWeights[(int)FaceParameter.EyeLookDownLeft] -= look.y;
                _outputWeights[(int)FaceParameter.EyeLookDownRight] -= look.y;
            }
        }

        private void WriteToAnimator()
        {
            for (int i = 0; i < _outputWeights.Length; i++)
            {
                if (_hasParameter[i])
                {
                    _animator.SetFloat(_parameterHashes[i], _outputWeights[i]);
                }
            }

            for (int i = 0; i < _visemeWeights.Length; i++)
            {
                if (_hasViseme[i])
                {
                    _animator.SetFloat(_visemeHashes[i], _visemeWeights[i]);
                }
            }
        }
    }
}
