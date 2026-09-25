namespace VSM.Presentation
{
    using System;
    using UnityEngine;
    using VSM.Interaction;
    /// <summary>Включает соседний вагон до открытия перехода и отключает его содержимое после полного закрытия дверей.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class VSMCarPortalVisibility : MonoBehaviour
    {
        [Serializable]
        public sealed class Car
        {
            [Tooltip("Основная геометрия и коллайдеры вагона.")] public GameObject body;
            [Tooltip("Потолок, откосы, мебельные коллайдеры и реквизит вагона.")] public GameObject details;
            [Tooltip("Продольная координата центра вагона в системе координат состава.")] public float centerZ;
        }
        [Serializable]
        public sealed class Portal
        {
            [Tooltip("Продольная координата середины перехода в системе координат состава.")] public float centerZ;
            [Tooltip("Полотна дверей с обеих сторон перехода.")] public VSMSlidingDoor[] doors;
        }
        [Tooltip("Корень игрока или XR Origin.")] public Transform passenger;
        [Tooltip("Вагоны в порядке следования.")] public Car[] cars;
        [Tooltip("Переходы между соседними вагонами.")] public Portal[] portals;
        [Min(2), Tooltip("Расстояние до середины перехода для предварительного включения обоих вагонов. Должно быть больше расстояния открытия двери.")]
        public float preloadDistance = 3.2f;
        bool[] visible;
        public int ActiveCarCount { get; private set; }
        /// <summary>Создаёт рабочий массив и задаёт начальное состояние вагонов.</summary>
        void Start() { visible = new bool[cars.Length]; RefreshVisibility(); }
        /// <summary>Проверяет переходы до обновления дверей, чтобы геометрия успела появиться перед открытием.</summary>
        void Update() { RefreshVisibility(); }
        /// <summary>Сохраняет текущий вагон и оба вагона открытого или приближающегося перехода.</summary>
        public void RefreshVisibility()
        {
            if (!passenger || cars == null || cars.Length == 0) return;
            if (visible == null || visible.Length != cars.Length) visible = new bool[cars.Length];
            float z = transform.InverseTransformPoint(passenger.position).z;
            int current = 0; float closest = float.MaxValue;
            for (int i = 0; i < cars.Length; i++) { visible[i] = false; float d = Mathf.Abs(z - cars[i].centerZ); if (d < closest) { closest = d; current = i; } }
            visible[current] = true;
            for (int i = 0; i < portals.Length && i + 1 < cars.Length; i++)
            {
                bool connected = Mathf.Abs(z - portals[i].centerZ) <= preloadDistance;
                foreach (var door in portals[i].doors)
                    if (door && door.gameObject.activeInHierarchy && (door.IsOpen || Vector3.Distance(door.transform.localPosition, door.closedPosition) > .002f)) connected = true;
                if (connected) { visible[i] = true; visible[i + 1] = true; }
            }
            ActiveCarCount = 0;
            for (int i = 0; i < cars.Length; i++) { if (visible[i]) ActiveCarCount++; SetActive(cars[i].body, visible[i]); SetActive(cars[i].details, visible[i]); }
        }
        /// <summary>Меняет активность корня только при фактическом изменении состояния.</summary>
        static void SetActive(GameObject root, bool value) { if (root && root.activeSelf != value) root.SetActive(value); }
        /// <summary>Возвращает все вагоны для редактирования после отключения системы.</summary>
        void OnDisable() { if (cars != null) foreach (var car in cars) { SetActive(car.body, true); SetActive(car.details, true); } }
    }
}
