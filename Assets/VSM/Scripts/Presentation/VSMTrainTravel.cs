namespace VSM.Presentation
{
    using UnityEngine;
    /// <summary>Управляет скоростью поезда и непрерывным смещением пейзажа: разгон, торможение и полная остановка.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class VSMTrainTravel : MonoBehaviour
    {
        [Min(0), Tooltip("Крейсерская скорость поезда в метрах в секунду. 80 м/с соответствует 288 км/ч.")]
        public float cruiseSpeed = 80;
        [Min(0), Tooltip("Целевая скорость в метрах в секунду. Ноль означает плавную остановку.")]
        public float targetSpeed = 80;
        [Min(.01f), Tooltip("Ускорение и замедление в метрах в секунду за секунду.")]
        public float acceleration = 2;
        [Min(0), Tooltip("Скорость сразу после запуска сцены в метрах в секунду.")]
        public float initialSpeed = 80;
        [Min(0), Tooltip("Масштаб движения силуэтов в шейдере окон относительно пройденного расстояния.")]
        public float landscapeScale = .18f;
        public float CurrentSpeed { get; private set; }
        public float SpeedRatio { get { return Mathf.Clamp01(CurrentSpeed / Mathf.Max(1, cruiseSpeed)); } }
        public double TravelDistance { get; private set; }
        /// <summary>Устанавливает стартовую скорость и обнуляет пройденный путь.</summary>
        void Awake() { CurrentSpeed = Mathf.Max(0, initialSpeed); TravelDistance = 0; Publish(); }
        /// <summary>Плавно приближает скорость к целевой и накапливает путь без скачков при торможении.</summary>
        void Update() { CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, Mathf.Max(0, targetSpeed), Mathf.Max(.01f, acceleration) * Time.deltaTime); TravelDistance += CurrentSpeed * Time.deltaTime; Publish(); }
        /// <summary>Передаёт пройденное расстояние в шейдер всех окон состава.</summary>
        void Publish() { Shader.SetGlobalFloat("_VSMTravelPhase", (float)(TravelDistance * landscapeScale)); }
        /// <summary>Задаёт целевую скорость в метрах в секунду для плавного изменения движения.</summary>
        public void SetTargetSpeed(float metresPerSecond) { targetSpeed = Mathf.Max(0, metresPerSecond); }
        /// <summary>Запускает плавное торможение до полной остановки.</summary>
        [ContextMenu("Плавно остановить поезд")]
        public void StopTrain() { SetTargetSpeed(0); }
        /// <summary>Запускает плавный разгон до крейсерской скорости.</summary>
        [ContextMenu("Разогнать до крейсерской скорости")]
        public void ResumeCruise() { SetTargetSpeed(cruiseSpeed); }
        /// <summary>Сбрасывает глобальный параметр пейзажа при закрытии сцены.</summary>
        void OnDisable() { Shader.SetGlobalFloat("_VSMTravelPhase", 0); }
    }
}
