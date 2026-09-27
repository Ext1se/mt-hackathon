namespace VSM.UI
{
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.XR;
    /// <summary>Разрешает автоповорот телефона и перестраивает интерфейс под ориентацию, безопасную область и тип устройства.</summary>
    public sealed class VSMResponsiveLayout : MonoBehaviour
    {
        [Tooltip("Контейнер интерфейса внутри безопасной области экрана.")] public RectTransform safeArea;
        [Tooltip("Область стика движения.")] public RectTransform joystick;
        [Tooltip("Кнопка взаимодействия.")] public RectTransform actionButton;
        [Tooltip("Текст подсказки над нижним краем экрана.")] public RectTransform hint;
        [Tooltip("Прозрачная область управления обзором.")] public RectTransform lookArea;
        [Tooltip("Показывать сенсорное управление также в редакторе для проверки.")] public bool previewTouchInEditor = true;
        [Tooltip("Масштабирование общего интерфейса.")] public CanvasScaler scaler;
        int width, height;
        Rect previousSafe;
        bool previousXR;
        /// <summary>Включает разрешённые ориентации и выполняет первоначальную раскладку.</summary>
        void OnEnable()
        {
            if (Application.isMobilePlatform) { Screen.autorotateToPortrait = true; Screen.autorotateToPortraitUpsideDown = true; Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true; Screen.orientation = ScreenOrientation.AutoRotation; }
            ApplyLayout();
        }
        /// <summary>Обновляет раскладку только при изменении размера, безопасной области или режима XR.</summary>
        void Update() { if (width != Screen.width || height != Screen.height || previousSafe != Screen.safeArea || previousXR != XRSettings.isDeviceActive) ApplyLayout(); }
        /// <summary>Применяет безопасные отступы и расположение элементов для текущей ориентации.</summary>
        public void ApplyLayout()
        {
            width = Screen.width; height = Screen.height; previousSafe = Screen.safeArea; previousXR = XRSettings.isDeviceActive;
            if (width <= 0 || height <= 0 || !safeArea) return;
            safeArea.anchorMin = previousSafe.min / new Vector2(width, height); safeArea.anchorMax = previousSafe.max / new Vector2(width, height); safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            bool portrait = height > width;
            if (scaler) { scaler.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f; }
            bool touch = !previousXR && (Application.isMobilePlatform || (Application.isEditor && previewTouchInEditor));
            var helpText = hint.GetComponentInChildren<Text>();
            if (helpText && (helpText.text.StartsWith("WASD") || helpText.text.StartsWith("Слева"))) helpText.text = touch ? "Слева: идти   •   справа: обзор   •   кнопка: действие" : "WASD: идти   •   мышь: обзор   •   E: взять / положить   •   F / ПКМ: курсор";
            var buttonText = actionButton.GetComponentInChildren<Text>(); if (buttonText) buttonText.text = touch ? "ДЕЙСТВИЕ" : "ДЕЙСТВИЕ  ·  E";
            joystick.gameObject.SetActive(touch); lookArea.gameObject.SetActive(touch); actionButton.gameObject.SetActive(!previousXR);
            SetRect(joystick, Vector2.zero, new Vector2(portrait ? 125 : 120, portrait ? 170 : 112), new Vector2(164, 164));
            SetRect(actionButton, new Vector2(1, 0), new Vector2(-124, portrait ? 166 : 106), new Vector2(190, 66));
            SetRect(hint, new Vector2(.5f, 0), new Vector2(0, portrait ? 295 : 30), new Vector2(portrait ? 650 : 850, 50));
            lookArea.anchorMin = new Vector2(.4f, .12f); lookArea.anchorMax = new Vector2(1, .9f); lookArea.offsetMin = lookArea.offsetMax = Vector2.zero;
            // В XR экранный интерфейс скрывается; камера и отслеживание головы остаются у XR-провайдера.
            safeArea.gameObject.SetActive(!previousXR);
        }
        /// <summary>Размещает элемент относительно заданной точки привязки.</summary>
        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
