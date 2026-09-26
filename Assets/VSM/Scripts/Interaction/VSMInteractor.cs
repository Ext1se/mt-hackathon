namespace VSM.Interaction
{
    using UnityEngine;
    using UnityEngine.UI;
    using VSM.Presentation;
    /// <summary>Обрабатывает подбор, безопасное размещение предметов и ручное открытие дверей.</summary>
    public sealed class VSMInteractor : MonoBehaviour
    {
        [Tooltip("Камера, из которой направляется луч взаимодействия.")] public Camera view;
        [Tooltip("Текст подсказок взаимодействия.")] public Text status;
        [Tooltip("Система вагонов для размещения предмета в текущем вагоне после переноса через переход.")]
        public VSMCarPortalVisibility carVisibility;
        VSMTaskProp held;
        Collider[] heldColliders;
        bool[] colliderStates;
        Quaternion originalLocalRotation;
        Vector3 originalLocalPosition, heldScale;
        Transform heldParent;
        /// <summary>Возвращает удерживаемый предмет при отключении игрока.</summary>
        void OnDisable() { if (held) ReturnHeld(); }
        /// <summary>Берёт предмет, кладёт его на свободную поверхность или переключает дверь.</summary>
        public void Interact()
        {
            RaycastHit hit;
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out hit, 2.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) { if (held) Hint("Наведите на пол или стол"); return; }
            if (held) { Place(hit); return; }
            var scenarioTarget = hit.collider.GetComponentInParent<Game.Scenarios.Presentation.World.IScenarioInteractable>();
            if (scenarioTarget != null) { scenarioTarget.Interact(); return; }
            var door = hit.collider.GetComponentInParent<VSMSlidingDoor>();
            if (door) { door.Toggle(); Hint(door.IsOpen ? "Дверь открывается" : "Дверь закрывается"); return; }
            var prop = hit.collider.GetComponentInParent<VSMTaskProp>();
            if (!prop) return;
            if (!prop.portable) { Hint(prop.displayName + " — объект задания"); return; }
            PickUp(prop);
        }
        /// <summary>Запоминает положение предмета и отключает его столкновения на время переноса.</summary>
        void PickUp(VSMTaskProp prop)
        {
            held = prop; heldParent = prop.transform.parent; heldScale = prop.transform.localScale;
            originalLocalPosition = prop.transform.localPosition; originalLocalRotation = prop.transform.localRotation;
            heldColliders = prop.GetComponentsInChildren<Collider>(); colliderStates = new bool[heldColliders.Length];
            for (int i = 0; i < heldColliders.Length; i++) { colliderStates[i] = heldColliders[i].enabled; heldColliders[i].enabled = false; }
            prop.transform.SetParent(view.transform, true); prop.transform.localPosition = new Vector3(.18f, -.26f, .65f); prop.transform.localRotation = Quaternion.identity;
            var mop = prop.GetComponent<VSMMopPose>(); if (mop) mop.Begin(view.transform);
            Hint(prop.displayName + " · поверхность + действие — положить");
        }
        /// <summary>Проверяет свободное место и размещает предмет в иерархии того вагона, куда его принесли.</summary>
        void Place(RaycastHit hit)
        {
            if (hit.normal.y < .65f) { Hint("Наведите на стол или пол, чтобы положить"); return; }
            Vector3 candidate = hit.point + hit.normal * .008f;
            Quaternion rotation = heldParent ? heldParent.rotation * originalLocalRotation : originalLocalRotation;
            var box = held.GetComponent<BoxCollider>();
            if (box)
            {
                Vector3 center = candidate + rotation * Vector3.Scale(box.center, heldScale);
                Vector3 extents = Vector3.Scale(box.size, heldScale) * .48f;
                foreach (var other in Physics.OverlapBox(center, extents, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (other != hit.collider && other.GetComponentInParent<VSMTaskProp>() != held) { Hint("Здесь недостаточно места для предмета"); return; }
            }
            var mop = held.GetComponent<VSMMopPose>(); if (mop) mop.End();
            Transform destination = heldParent;
            if (carVisibility && carVisibility.cars != null)
            {
                float z = carVisibility.transform.InverseTransformPoint(candidate).z, nearest = float.MaxValue;
                foreach (var car in carVisibility.cars) { float d = Mathf.Abs(z - car.centerZ); if (d < nearest) { nearest = d; destination = car.details.transform; } }
            }
            held.transform.SetParent(destination, true); held.transform.localScale = heldScale; held.transform.SetPositionAndRotation(candidate, rotation);
            RestoreColliders(); held = null; Hint("Предмет размещён");
        }
        /// <summary>Возвращает предмет в исходное локальное положение, учитывая движение его вагона.</summary>
        void ReturnHeld()
        {
            var mop = held.GetComponent<VSMMopPose>(); if (mop) mop.End();
            held.transform.SetParent(heldParent, false); held.transform.localScale = heldScale;
            held.transform.localPosition = originalLocalPosition; held.transform.localRotation = originalLocalRotation;
            RestoreColliders(); held = null;
        }
        /// <summary>Восстанавливает исходное состояние каждого коллайдера предмета.</summary>
        void RestoreColliders() { for (int i = 0; i < heldColliders.Length; i++) if (heldColliders[i]) heldColliders[i].enabled = colliderStates[i]; }
        /// <summary>Обновляет подсказку только при выполнении действия.</summary>
        void Hint(string message) { if (status) status.text = message; }
    }
}
