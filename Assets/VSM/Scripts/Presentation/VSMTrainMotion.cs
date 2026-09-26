namespace VSM.Presentation
{
    using UnityEngine;

    /// <summary>Плавно покачивает весь состав вместе с коллайдерами и переносит пассажира движением пола; не меняет поворот камеры.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class VSMTrainMotion : MonoBehaviour
    {
        [Tooltip("Корень пассажира или XR Origin, который переносится вместе с полом.")]
        public Transform passenger;
        [Tooltip("Качать ли состав. Выключено: вагоны стоят неподвижно, окна без поворота пейзажа.")]
        public bool swayEnabled;
        [Tooltip("Максимальный крен состава в градусах.")]
        public float rollDegrees = .22f;
        [Tooltip("Небольшое изменение курса в плавном повороте, в градусах.")]
        public float turnDegrees = .38f;
        [Tooltip("Интервал между плавными поворотами в секундах.")]
        public float turnPeriod = 55f;
        [Tooltip("Вертикальное покачивание пола в метрах.")]
        public float heave = .003f;
        Vector3 origin;
        Quaternion initialRotation;
        CharacterController controller;
        VSMTrainTravel travel;
        public float Curve { get; private set; }

        /// <summary>Запоминает исходное положение состава и контроллер пассажира.</summary>
        void Awake() { travel = GetComponent<VSMTrainTravel>(); origin = transform.position; initialRotation = transform.rotation; if (passenger) controller = passenger.GetComponent<CharacterController>(); }

        /// <summary>Перемещает состав и компенсирует перемещение опоры под пассажиром, сохраняя независимый поворот головы.</summary>
        void Update()
        {
            if (!swayEnabled) { Rest(); return; }
            Vector3 localPassenger = passenger ? transform.InverseTransformPoint(passenger.position) : Vector3.zero;
            float t = Time.time;
            float phase = (t / Mathf.Max(20f, turnPeriod)) * Mathf.PI * 2f;
            Curve = Mathf.Sin(phase) * Mathf.Pow(Mathf.Abs(Mathf.Sin(phase)), 3f);
            float speedRatio = travel ? travel.SpeedRatio : 1;
            Curve *= speedRatio;
            float roll = speedRatio * rollDegrees * (.6f * Mathf.Sin(t * 1.37f) + .4f * Mathf.Sin(t * .71f)) + Curve * rollDegrees;
            transform.SetPositionAndRotation(origin + Vector3.up * (speedRatio * heave * Mathf.Sin(t * 1.8f)), initialRotation * Quaternion.Euler(0, Curve * turnDegrees, roll));
            if (passenger)
            {
                // Компенсация переносит только корень игрока. Поворот отслеживаемой головы не изменяется.
                Vector3 target = transform.TransformPoint(localPassenger);
                if (controller) { bool active = controller.enabled; controller.enabled = false; passenger.position = target; controller.enabled = active; }
                else passenger.position = target;
            }
            Shader.SetGlobalFloat("_VSMCurve", Curve);
            Physics.SyncTransforms();
        }

        /// <summary>Возвращает состав в исходное положение и сбрасывает параметр поворота окон.</summary>
        void OnDisable() { transform.SetPositionAndRotation(origin, initialRotation); Shader.SetGlobalFloat("_VSMCurve", 0); }

        /// <summary>Держит состав в исходном положении, пока качка выключена; флаг можно переключать во время игры.</summary>
        void Rest()
        {
            if (Curve == 0 && transform.position == origin && transform.rotation == initialRotation) return;
            Curve = 0; transform.SetPositionAndRotation(origin, initialRotation); Shader.SetGlobalFloat("_VSMCurve", 0); Physics.SyncTransforms();
        }
    }
}
