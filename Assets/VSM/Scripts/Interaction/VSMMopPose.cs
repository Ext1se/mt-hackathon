namespace VSM.Interaction
{
    using UnityEngine;
    /// <summary>При удержании размещает рабочую часть швабры у пола и наклоняет рукоять к пассажиру.</summary>
    public sealed class VSMMopPose : MonoBehaviour
    {
        [Tooltip("Расстояние от пассажира до рабочей части швабры в метрах.")] public float reach = .85f;
        [Tooltip("Наклон рукояти к пассажиру в градусах.")] public float lean = 18;
        Transform view;
        /// <summary>Включает специальную позу удержания относительно выбранной камеры.</summary>
        public void Begin(Transform cameraTransform) { view = cameraTransform; }
        /// <summary>Отключает специальную позу перед размещением предмета.</summary>
        public void End() { view = null; }
        /// <summary>Находит пол впереди пассажира и сохраняет касание рабочей части с поверхностью.</summary>
        void LateUpdate()
        {
            if (!view) return;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 probe = view.position + forward * reach;
            RaycastHit hit;
            if (Physics.Raycast(probe, Vector3.down, out hit, 2.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * .018f;
                transform.rotation = Quaternion.LookRotation(forward, hit.normal) * Quaternion.Euler(-lean, 0, 0);
            }
        }
    }
}
