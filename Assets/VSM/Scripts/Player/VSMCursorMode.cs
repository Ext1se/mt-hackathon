namespace VSM.Player
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.XR;
    /// <summary>Скрывает и фиксирует курсор на ПК; временно освобождает его для окон интерфейса или по клавише F / ПКМ.</summary>
    public sealed class VSMCursorMode : MonoBehaviour
    {
        public bool UIIsOpen { get; private set; }
        /// <summary>Курсор освобождён игроком (F / ПКМ): обзор камеры заблокирован, пока курсор снова не захвачен.</summary>
        public bool CursorFree { get; private set; }
        bool focused = true;
        InputAction toggle;
        /// <summary>Находит действие переключения курсора в наборе действий ходьбы.</summary>
        void Awake() { var walk = GetComponent<VSMWalkController>(); if (walk && walk.actions) toggle = walk.actions.FindAction("Walk/Cursor", false); }
        /// <summary>Включает управление от первого лица при запуске сцены.</summary>
        void Start() { Apply(); }
        /// <summary>Переключает свободный курсор и возвращает захват после системного освобождения нажатием внутри окна игры.</summary>
        void Update()
        {
            if (Application.isMobilePlatform || XRSettings.isDeviceActive || UIIsOpen) return;
            if (toggle != null && toggle.WasPressedThisFrame()) { CursorFree = !CursorFree; Apply(); return; }
            // Щелчок по окну возвращает захват только в обычном режиме: со свободным курсором щелчки идут в интерфейс.
            if (!CursorFree && focused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Apply();
        }
        /// <summary>Освобождает курсор и блокирует ходьбу для открытого UI; восстанавливает управление после закрытия.</summary>
        public void SetUIOpen(bool open) { UIIsOpen = open; Apply(); }
        /// <summary>Учитывает потерю и возврат фокуса окна приложения.</summary>
        void OnApplicationFocus(bool value) { focused = value; Apply(); }
        /// <summary>Применяет режим курсора только на ПК без шлема.</summary>
        void Apply() { bool capture = focused && !UIIsOpen && !CursorFree && !Application.isMobilePlatform && !XRSettings.isDeviceActive; Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }
        /// <summary>Освобождает курсор при выходе из сцены.</summary>
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
