namespace VSM.Player
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;
    using UnityEngine.XR;
    using VSM.Input;
    using VSM.Interaction;
    using UnityEngine.Scripting.APIUpdating;
    /// <summary>Перемещает пассажира с клавиатуры, геймпада или стика; передаёт взаимодействие отдельному компоненту.</summary>
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "VSMWalkController")]
    [RequireComponent(typeof(CharacterController), typeof(VSMInteractor))]
    public sealed class VSMWalkController : MonoBehaviour, Game.Scenarios.Presentation.World.IViewFocus
    {
        [Tooltip("Камера пассажира; в XR её поворот задаёт только отслеживание головы.")] public Camera view;
        [Tooltip("Действия Walk для движения, обзора и взаимодействия.")] public InputActionAsset actions;
        [Tooltip("Сенсорный стик движения.")] public VSMTouchPad movePad;
        [Tooltip("Сенсорная область обзора.")] public VSMTouchPad lookPad;
        [Tooltip("Поле подсказок игроку.")] public Text status;
        [SerializeField, Tooltip("Скорость ходьбы, метры в секунду.")] float speed = 1.65f;
        [SerializeField, Tooltip("Скорость бега при зажатом Shift, метры в секунду.")] float runSpeed = 3.3f;
        [Tooltip("Разрешить бег по Shift (левый стик геймпада — нажатие).")] public bool allowRunning = true;
        [SerializeField, Tooltip("Скорость поворота взгляда к собеседнику в диалоге.")] float focusTurnSpeed = 5f;
        CharacterController motor;
        InputAction move, look, interact, sprint;
        VSMInteractor interactor;
        float pitch, vertical;
        VSMCursorMode cursorMode;
        Vector3 focusPoint; bool hasFocus;
        /// <summary>Сохраняет ссылки на компоненты и подключает действия ввода.</summary>
        void Awake()
        {
            cursorMode = GetComponent<VSMCursorMode>(); motor = GetComponent<CharacterController>(); interactor = GetComponent<VSMInteractor>();
            interactor.view = view; interactor.status = status;
            move = actions.FindAction("Walk/Move"); look = actions.FindAction("Walk/Look"); interact = actions.FindAction("Walk/Interact");
            sprint = actions.FindAction("Walk/Sprint", false);
            gameObject.layer = 2;
            if (!XRSettings.isDeviceActive) Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;
            pitch = Mathf.DeltaAngle(0, view.transform.localEulerAngles.x);
        }
        /// <summary>Включает набор действий игрока.</summary>
        void OnEnable() { if (actions) actions.Enable(); }
        /// <summary>Отключает набор действий игрока.</summary>
        void OnDisable() { if (actions) actions.Disable(); }
        /// <summary>Читает ввод, перемещает персонажа и обрабатывает запрос взаимодействия.</summary>
        void Update()
        {
            if (cursorMode && cursorMode.UIIsOpen) { if (hasFocus && !XRSettings.isDeviceActive) TurnToFocus(); return; }
            Vector2 v = move.ReadValue<Vector2>(); if (movePad && movePad.Value.sqrMagnitude > 0) v = movePad.Value;
            v = Vector2.ClampMagnitude(v, 1);
            if (!XRSettings.isDeviceActive) UpdateLook();
            vertical = motor.isGrounded ? -1 : Mathf.Max(-15, vertical - 18 * Time.deltaTime);
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float currentSpeed = allowRunning && sprint != null && sprint.IsPressed() ? runSpeed : speed;
            if (motor.enabled) motor.Move(((forward * v.y + right * v.x) * currentSpeed + Vector3.up * vertical) * Time.deltaTime);
            if (interact.WasPressedThisFrame()) Interact();
        }
        /// <summary>Поворачивает обзор мышью, геймпадом или касанием только вне XR.</summary>
        void UpdateLook()
        {
            Vector2 delta = Vector2.zero;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked) delta = look.ReadValue<Vector2>() * .09f;
            if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * (90 * Time.deltaTime);
            if (lookPad) delta += lookPad.ConsumeLook() * (100f / Screen.height);
            transform.Rotate(0, delta.x, 0); pitch = Mathf.Clamp(pitch - delta.y, -70, 70);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        /// <summary>Плавно поворачивает корпус и камеру к точке фокуса, пока открыт диалог.</summary>
        void TurnToFocus()
        {
            Vector3 to = focusPoint - view.transform.position; Vector3 flat = new Vector3(to.x, 0, to.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            float blend = 1 - Mathf.Exp(-focusTurnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), blend);
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -70, 70);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        /// <summary>Запоминает, куда смотреть во время диалога.</summary>
        public void FocusOn(Vector3 point) { focusPoint = point; hasFocus = true; }
        /// <summary>Возвращает свободный обзор.</summary>
        public void ClearFocus() { hasFocus = false; }
        /// <summary>Бежит ли пассажир в этом кадре.</summary>
        public bool IsRunning { get { return allowRunning && sprint != null && sprint.IsPressed(); } }
        /// <summary>Передаёт событие кнопки отдельному компоненту взаимодействия.</summary>
        public void Interact() { if (interactor) interactor.Interact(); }
    }
}
