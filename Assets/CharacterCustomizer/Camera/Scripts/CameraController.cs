using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CC
{
    [DefaultExecutionOrder(100)]
    public class CameraController : MonoBehaviour
    {
        public static CameraController instance;

        public delegate void OnHover(string partHovered);

        public event OnHover onHover;

        public delegate void OnDrag(string partX, string partY, float deltaX, float deltaY, bool first);

        public event OnDrag onDrag;

        [Header("Drag Settings")]
        public float dragScale = 0.01f;
        public Transform canvasParent;
        private Canvas canvas;

        private string hoveredPart = "";
        private string partX, partY = "";
        private float multX, multY = 1f;

        private Camera _camera;
        private Transform cameraRoot;

        private Vector2 mousePosition;
        private Vector2 mouseDelta;
        private float scrollDelta;

        private Vector3 cameraRotationTarget = new Vector3(10, -5, 0);
        private Vector3 cameraRotationDefault;

        private Vector3 cameraOffset;
        private Vector2 panOffset;

        private float headAdjust = 0f;

        [Header("Camera Settings")]
        public float ZoomMin = -0.6f;
        public float ZoomMax = -3.1f;
        public float ZoomPanScale = 0.5f;
        public float zoomTarget = 0.5f;
        public float rotateSpeed = 5;
        public float panSpeed = 3;
        public Vector3 cameraOffsetMin = new Vector3(-0.15f, 0.5f, 0);
        public Vector3 cameraOffsetMax = new Vector3(-0.3f, -0.1f, 0);

        public float defaultHeadLevel = 1.8f;
        public GameObject headLevelObject;

        private bool panning = false;
        private bool rotating = false;
        private bool dragging = false;
        private bool resetting = false;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _camera = GetComponentInChildren<Camera>(true);

            cameraRoot = gameObject.transform;

            cameraRotationDefault = cameraRoot.localRotation.eulerAngles;
            cameraRotationTarget = cameraRotationDefault;
        }

        private void LateUpdate()
        {
            setHeadLevel();

            //Drag
            if (dragging) return;
            onHover?.Invoke(hoveredPart);

            Physics.SyncTransforms();

            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit) && !EventSystem.current.IsPointerOverGameObject())
            {
                hoveredPart = hit.collider.name;
            }
            else hoveredPart = "";
        }

        private void Update()
        {
            if (canvas == null) getCanvas();
            if (!_camera.gameObject.activeSelf || canvas == null) return;

            //Compatibility friendly input fetching
#if ENABLE_LEGACY_INPUT_MANAGER
            mouseDelta = (mousePosition - (Vector2)Input.mousePosition) / canvas.scaleFactor;
            mousePosition = Input.mousePosition;
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                scrollDelta = Input.mouseScrollDelta.y;
                if (Input.GetMouseButtonDown(1)) rotating = true;
                if (Input.GetMouseButtonDown(2)) panning = true;
            }
            resetting = Input.GetKeyDown(KeyCode.F);
#else
            mouseDelta = (mousePosition - Mouse.current.position.ReadValue()) / canvas.scaleFactor;
            mousePosition = Mouse.current.position.ReadValue();
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                scrollDelta = Mouse.current.scroll.ReadValue().y;
                if (Mouse.current.rightButton.wasPressedThisFrame) rotating = true;
                if (Mouse.current.middleButton.wasPressedThisFrame) panning = true;
            }
            resetting = Keyboard.current.fKey.wasPressedThisFrame;
#endif

            //Set rotating/panning when we're not hovering over anything
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                //Set zoom target
                if (scrollDelta < 0)
                {
                    zoomTarget = Mathf.Clamp(zoomTarget * 1.2f, 0.05f, 1f);
                }
                else if (scrollDelta > 0)
                {
                    zoomTarget = Mathf.Clamp01(zoomTarget * 0.8f);
                }

                if (scrollDelta != 0) panOffset = Vector3.Lerp(panOffset, Vector3.zero, 0.1f);
            }

            //Rotation
            if (rotating)
            {
                cameraRotationTarget.x = cameraRotationTarget.x + mouseDelta.y / 5;
                cameraRotationTarget.y = cameraRotationTarget.y - mouseDelta.x / 5;
            }

            //Panning
            if (panning)
            {
                panOffset -= mouseDelta / 500;
            }

            //Reset camera with F
            if (resetting)
            {
                resetCamera();
            }

            //Camera XY
            cameraOffset = Vector2.Lerp(cameraOffsetMin, cameraOffsetMax, zoomTarget) + panOffset;
            //Camera Z offset i.e zoom
            cameraOffset.z = Mathf.Lerp(ZoomMin, ZoomMax, Mathf.Clamp01(zoomTarget));
            //Camera height adjust based on head level
            cameraOffset.y -= headAdjust;

            //Camera offset interp
            _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition, cameraOffset, Time.deltaTime * panSpeed);

            //Rotation interp
            if (rotateSpeed == 0)
            {
                cameraRoot.transform.localRotation = Quaternion.Euler(cameraRotationTarget);
            }
            else
            {
                cameraRoot.transform.localRotation = Quaternion.Slerp(cameraRoot.transform.localRotation, Quaternion.Euler(cameraRotationTarget), Time.deltaTime * rotateSpeed);
            }

            //Drag update
            dragUpdate();

            //Release input
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonUp(1)) rotating = false;
            if (Input.GetMouseButtonUp(2)) panning = false;
#else
            if (Mouse.current.rightButton.wasReleasedThisFrame) rotating = false;
            if (Mouse.current.middleButton.wasReleasedThisFrame) panning = false;
#endif
            scrollDelta = 0f;
        }

        private void dragUpdate()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            bool first = Input.GetMouseButtonDown(0);
            dragging = Input.GetMouseButton(0);
#else
            bool first = Mouse.current.leftButton.wasPressedThisFrame;
            dragging = Mouse.current.leftButton.isPressed;
#endif

            //Set shape on first drag
            if (first)
            {
                partX = ""; partY = "";
                multX = hoveredPart.Contains("_r") ? 1 : -1; multY = 1f;

                if (hoveredPart.Contains("spine_05")) { partX = "BodyCustomization_ShoulderWidth"; partY = "BodyCustomization_TorsoHeight"; }
                else if (hoveredPart.Contains("spine")) { partX = "BodyCustomization_WaistSize"; partY = ""; }
                else if (hoveredPart.Contains("pelvis")) { partX = "BodyCustomization_HipWidth"; partY = ""; }
                else if (hoveredPart.Contains("lowerarm")) { partX = "BodyCustomization_LowerArmScale"; partY = ""; }
                else if (hoveredPart.Contains("upperarm")) { partX = "BodyCustomization_UpperArmScale"; partY = ""; }
                else if (hoveredPart.Contains("thigh")) { partX = "BodyCustomization_ThighScale"; partY = ""; }
                else if (hoveredPart.Contains("calf")) { partX = "BodyCustomization_CalfScale"; partY = ""; }
                else if (hoveredPart.Contains("head")) { partX = "BodyCustomization_HeadSize"; partY = "BodyCustomization_NeckLength"; }
                else if (hoveredPart.Contains("neck")) { partX = "BodyCustomization_NeckScale"; partY = "BodyCustomization_NeckLength"; }
                else if (hoveredPart.Contains("collider_nose")) { partX = "mod_nose_size"; partY = "mod_nose_height"; multY = -1; }
                else if (hoveredPart.Contains("collider_mouth")) { partX = "mod_mouth_size"; partY = "mod_mouth_height"; multY = -1; }
                else if (hoveredPart.Contains("collider_cheekbones")) { partX = "mod_cheekbone_size"; partY = ""; multX *= -1; }
                else if (hoveredPart.Contains("collider_cheeks")) { partX = "mod_cheeks_size"; partY = ""; multX *= -1; }
                else if (hoveredPart.Contains("collider_jaw")) { partX = "mod_jaw_width"; partY = "mod_jaw_height"; multX *= -1; }
                else if (hoveredPart.Contains("collider_chin")) { partX = ""; partY = "mod_chin_size"; }
                else if (hoveredPart.Contains("collider_eye")) { partX = "mod_eyes_narrow"; partY = "mod_eyes_height"; multY = -1; }
                else if (hoveredPart.Contains("collider_brow")) { partX = ""; partY = "mod_brow_height"; }
            }

            if (dragging)
            {
                onDrag?.Invoke(partX, partY, mouseDelta.x * multX * dragScale, mouseDelta.y * multY * dragScale, first);
            }
        }

        private void getCanvas()
        {
            if (canvas == null)
            {
                canvas = canvasParent.GetComponentInChildren<Canvas>();
            }
        }

        public void resetCamera()
        {
            cameraRotationTarget = cameraRotationDefault;
            panOffset = Vector3.zero;
            zoomTarget = 0.5f;
        }

        private void setHeadLevel()
        {
            if (headLevelObject == null || !headLevelObject.activeInHierarchy)
            {
                headLevelObject = GameObject.FindGameObjectWithTag("HeadLevel");
            }
            if (headLevelObject == null)
            {
                headAdjust = 0f;
                return;
            }
            headAdjust = defaultHeadLevel - (headLevelObject.transform.position.y - transform.parent.position.y);
        }
    }
}