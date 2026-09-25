namespace VSM.Interaction
{
    using UnityEngine;
    /// <summary>Открывает дверь вдоль стены без пересечения полотна с перегородкой; проходные двери открываются автоматически.</summary>
    public sealed class VSMSlidingDoor : MonoBehaviour
    {
        [Tooltip("Положение полотна при закрытой двери в координатах родителя.")] public Vector3 closedPosition;
        [Tooltip("Положение полотна при открытой двери в координатах родителя.")] public Vector3 openPosition;
        [Tooltip("Пассажир для автоматического открытия проходных дверей.")] public Transform passenger;
        [Tooltip("Включить автоматическое открытие при приближении пассажира.")] public bool automatic;
        [Tooltip("Скорость движения полотна в метрах в секунду.")] public float speed = 1.1f;
        [Tooltip("Звук начала движения двери. У парных створок назначается только одной створке.")] public AudioClip movementClip;
        [Range(0f,1f), Tooltip("Громкость звука движения двери.")] public float soundVolume = .55f;
        AudioSource movementSource;
        bool previousRequest;
        bool requested;
        public bool IsOpen { get { return requested; } }
        /// <summary>Переключает ручную дверь при нажатии кнопки взаимодействия.</summary>
        public void Toggle() { requested = !requested; }
        /// <summary>Создаёт пространственный источник звука рядом с дверью.</summary>
        void Awake()
        {
            if (!movementClip) return;
            movementSource = GetComponent<AudioSource>();
            if (!movementSource) movementSource = gameObject.AddComponent<AudioSource>();
            movementSource.playOnAwake = false;
            movementSource.spatialBlend = 1f;
            movementSource.minDistance = 1f;
            movementSource.maxDistance = 7f;
            movementSource.rolloffMode = AudioRolloffMode.Linear;
        }
        /// <summary>Выбирает состояние проходной двери и плавно перемещает её полотно.</summary>
        void Update()
        {
            if (automatic && passenger) { Vector3 center = transform.parent.TransformPoint(closedPosition); Vector3 delta = passenger.position - center; requested = new Vector2(delta.x, delta.z).magnitude < 1.55f; }
            if (previousRequest != requested)
            {
                if (movementSource && movementClip) { movementSource.Stop(); movementSource.PlayOneShot(movementClip, soundVolume); }
                previousRequest = requested;
            }
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, requested ? openPosition : closedPosition, speed * Time.deltaTime);
        }
    }
}
