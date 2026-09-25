namespace VSM.Player
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.XR;
    /// <summary>Скрывает и фиксирует курсор на ПК; временно освобождает его для будущих окон интерфейса.</summary>
    public sealed class VSMCursorMode : MonoBehaviour
    {
        public bool UIIsOpen { get; private set; }
        bool focused = true;
        /// <summary>Включает управление от первого лица при запуске сцены.</summary>
        void Start() { Apply(); }
        /// <summary>Возвращает захват после системного освобождения курсора нажатием внутри окна игры.</summary>
        void Update() { if (!UIIsOpen && focused && !Application.isMobilePlatform && !XRSettings.isDeviceActive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Apply(); }
        /// <summary>Освобождает курсор и блокирует ходьбу для открытого UI; восстанавливает управление после закрытия.</summary>
        public void SetUIOpen(bool open) { UIIsOpen = open; Apply(); }
        /// <summary>Учитывает потерю и возврат фокуса окна приложения.</summary>
        void OnApplicationFocus(bool value) { focused = value; Apply(); }
        /// <summary>Применяет режим курсора только на ПК без шлема.</summary>
        void Apply() { bool capture = focused && !UIIsOpen && !Application.isMobilePlatform && !XRSettings.isDeviceActive; Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }
        /// <summary>Освобождает курсор при выходе из сцены.</summary>
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
