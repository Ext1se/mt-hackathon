namespace VSM.Player
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.XR;
    using UnityEngine.XR;
    /// <summary>Передаёт камеру отслеживанию головы при запуске установленного XR-провайдера; сохраняет обычную камеру для ПК и телефона.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class VSMXRViewAdapter : MonoBehaviour
    {
        [Tooltip("Камера пассажира для обычного и стереоскопического режима.")] public Camera view;
        [Tooltip("Промежуточный контейнер камеры для высоты при отслеживании относительно устройства.")] public Transform trackingSpace;
        [Tooltip("Высота глаз при отсутствии отслеживания относительно пола, в метрах.")] public float seatedOriginHeight = 1.62f;
        TrackedPoseDriver driver;
        bool active;
        Vector3 defaultPosition;
        Quaternion defaultRotation;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        /// <summary>Подключает действия позы шлема, не включая отслеживание на обычном экране.</summary>
        void Awake()
        {
            defaultPosition = view.transform.localPosition; defaultRotation = view.transform.localRotation;
            driver = view.gameObject.AddComponent<TrackedPoseDriver>(); driver.enabled = false;
            driver.positionInput = new InputActionProperty(new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("HeadTrackingState", InputActionType.Value, "<XRHMD>/trackingState"));
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            ApplyMode(XRSettings.isDeviceActive);
        }
        /// <summary>Переключает режим только при подключении или отключении XR-устройства.</summary>
        void Update() { if (active != XRSettings.isDeviceActive) ApplyMode(XRSettings.isDeviceActive); }
        /// <summary>Выбирает начало отслеживания и включает драйвер головы без искусственного покачивания камеры.</summary>
        void ApplyMode(bool useXR)
        {
            active = useXR;
            if (useXR)
            {
                bool floor = false; SubsystemManager.GetSubsystems(subsystems);
                foreach (var subsystem in subsystems) if (subsystem.running) floor |= subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                trackingSpace.localPosition = floor ? Vector3.zero : Vector3.up * seatedOriginHeight;
                view.transform.localPosition = Vector3.zero; view.transform.localRotation = Quaternion.identity; driver.enabled = true;
            }
            else { driver.enabled = false; trackingSpace.localPosition = Vector3.zero; view.transform.localPosition = defaultPosition; view.transform.localRotation = defaultRotation; }
        }
        /// <summary>Выключает созданный драйвер при завершении работы компонента.</summary>
        void OnDisable() { if (driver) driver.enabled = false; }
        /// <summary>Освобождает действия отслеживания головы при выгрузке сцены.</summary>
        void OnDestroy() { if (driver) { driver.positionInput.action?.Dispose(); driver.rotationInput.action?.Dispose(); driver.trackingStateInput.action?.Dispose(); } }
    }
}
