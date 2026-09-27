using System.Collections;
using System.Collections.Generic;
using Game.Characters.Face;
using Game.Characters.Passengers;
using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>
    /// A scenario character: speaks with lip sync, shows emotions and can take a fixed seat.
    /// Runs before PassengerSpawner so its seat is already occupied when the wagon is filled.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ScenarioActor : MonoBehaviour
    {
        private const float SecondsPerCharacter = 0.06f;
        private const float MinTalkSeconds = 1f;
        private const float MaxTalkSeconds = 6f;

        // CharacterCustomizer lives in Assembly-CSharp, which an asmdef cannot reference, so the randomizer is called by name.
        private const string FearEmotion = "fear";
        private const string RandomizeBodyMessage = "randomizeAll";
        private const string RandomizeOutfitMessage = "setRandomOutfit";
        private const float SeatedHeadHeight = 1.2f;

        [SerializeField] private ScenarioRunner _runner;
        [Tooltip("Speaker id used in scenario JSON, e.g. passenger_12V.")]
        [SerializeField] private string _actorId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [Tooltip("Optional: lip sync and expressions.")]
        [SerializeField] private FaceController _face;
        [SerializeField] private List<EmotionBinding> _emotions = new List<EmotionBinding>();
        [Tooltip("How strongly this character shows emotions: 1 is the full expression, lower is subtler.")]
        [SerializeField, Range(0f, 1f)] private float _emotionIntensity = 1f;
        [Tooltip("Upper limit for the fear expression, whatever the intensity; the cast file sets it for everyone.")]
        [SerializeField, Range(0f, 1f)] private float _fearLimit = 1f;
        [Tooltip("Optional: the actor is hidden while a scenario plays another story variant.")]
        [SerializeField] private string _onlyInVariant = string.Empty;
        [Tooltip("Randomize body and outfit through CharacterCustomizer on start.")]
        [SerializeField] private bool _randomizeAppearance = true;

        [Header("Seat")]
        [Tooltip("Optional: the passenger takes this spot on start instead of a random one.")]
        [SerializeField] private Passenger _passenger;
        [SerializeField] private PassengerSpot _seat;
        [SerializeField] private PassengerPose _pose = PassengerPose.Sitting;
        [Tooltip("Optional: clip name prefix the passenger keeps playing (e.g. Sit_ImpatientWaiting) instead of varying.")]
        [SerializeField] private string _holdClip = string.Empty;

        private Animator _animator;

        public string ActorId => _actorId;
        public string DisplayName => _displayName;

        // Registered for the whole lifetime, not only while active: a culled wagon must not lose its speakers' names.
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_runner != null)
            {
                _runner.Register(this);
            }
        }

        private void Start()
        {
            // CharacterCustomizer's PhysicsManager disables every collider of the character in Awake (ragdoll off);
            // the interaction collider on the root must stay on so the player's ray can hit the actor.
            Collider interaction = GetComponent<Collider>();
            if (interaction != null)
            {
                interaction.enabled = true;
            }

            if (_passenger != null && _holdClip.Length > 0)
            {
                _passenger.HoldClip(_holdClip);
            }

            if (_passenger != null && _seat != null)
            {
                _passenger.TakeSpot(_seat, _pose);
            }

            if (_randomizeAppearance && _passenger != null)
            {
                StartCoroutine(RandomizeAppearance());
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Unregister(this);
            }
        }

        /// <summary>Lip sync for the spoken part of a line; italic parts are actions and narration, not speech.</summary>
        public void Speak(string text)
        {
            if (_face == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            int spoken = SpokenLength(text);
            if (spoken > 0)
            {
                _face.Talk(Mathf.Clamp(spoken * SecondsPerCharacter, MinTalkSeconds, MaxTalkSeconds));
            }
        }

        public void SetEmotion(string emotion)
        {
            if (_face == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(emotion))
            {
                _face.ClearExpression();
                return;
            }

            foreach (EmotionBinding binding in _emotions)
            {
                if (binding.Emotion == emotion)
                {
                    float intensity = emotion == FearEmotion ? Mathf.Min(_emotionIntensity, _fearLimit) : _emotionIntensity;
                    _face.SetExpression(binding.Expression, intensity);
                    return;
                }
            }
        }

        /// <summary>Where the player should look when this actor speaks; false for voices without a body (radio).</summary>
        public bool TryGetFocusPoint(out Vector3 point)
        {
            if (_passenger == null && _face == null)
            {
                point = Vector3.zero;
                return false;
            }

            if (_animator != null && _animator.isHuman)
            {
                Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    point = head.position;
                    return true;
                }
            }

            point = transform.position + Vector3.up * SeatedHeadHeight;
            return true;
        }

        /// <summary>Hides the actor when the playthrough picked a variant it does not belong to.</summary>
        public void OnScenarioStarted(string variantId)
        {
            if (_onlyInVariant.Length > 0 && _onlyInVariant != variantId)
            {
                gameObject.SetActive(false);
            }
        }

        public void OnScenarioEnded()
        {
            if (_onlyInVariant.Length > 0 && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            SetEmotion(string.Empty);
        }

        // Letters outside <i>...</i>; other rich-text tags are skipped too.
        private static int SpokenLength(string text)
        {
            int count = 0;
            int italicDepth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        string tag = text.Substring(i + 1, close - i - 1);
                        if (tag == "i")
                        {
                            italicDepth++;
                        }
                        else if (tag == "/i" && italicDepth > 0)
                        {
                            italicDepth--;
                        }

                        i = close;
                        continue;
                    }
                }

                if (italicDepth == 0 && char.IsLetter(text[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator RandomizeAppearance()
        {
            // CharacterCustomizer initializes in its own Start; randomizing before that is ignored.
            yield return null;
            SendMessage(RandomizeBodyMessage, SendMessageOptions.DontRequireReceiver);
            SendMessage(RandomizeOutfitMessage, SendMessageOptions.DontRequireReceiver);
        }
    }
}
