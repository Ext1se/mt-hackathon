namespace VSM.Input
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.Scripting.APIUpdating;
    /// <summary>Преобразует касания в движение стика или смещение обзора и отображает положение ручки.</summary>
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "VSMTouchPad")]
    public sealed class VSMTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Включено для области обзора; выключено для стика движения.")] public bool lookPad;
        [Tooltip("Визуальная ручка стика; у области обзора не задаётся.")] public RectTransform handle;
        public Vector2 Value { get; private set; }
        int pointer = -999;
        /// <summary>Захватывает только одно касание и сразу обновляет стик.</summary>
        public void OnPointerDown(PointerEventData e) { if (pointer != -999) return; pointer = e.pointerId; Value = Vector2.zero; if (!lookPad) OnDrag(e); }
        /// <summary>Вычисляет направление относительно центра стика или накапливает смещение обзора.</summary>
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            if (lookPad) { Value += e.delta; return; }
            var rect = (RectTransform)transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out local);
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * .32f;
            Value = Vector2.ClampMagnitude(local / Mathf.Max(1, radius), 1);
            if (handle) handle.anchoredPosition = Value * radius;
        }
        /// <summary>Сбрасывает стик при отпускании активного пальца.</summary>
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) ResetInput(); }
        /// <summary>Возвращает накопленное смещение обзора и очищает его.</summary>
        public Vector2 ConsumeLook() { Vector2 result = Value; if (lookPad) Value = Vector2.zero; return result; }
        /// <summary>Прекращает ввод при отключении или смене компоновки.</summary>
        void OnDisable() { ResetInput(); }
        /// <summary>Возвращает ручку в центр и освобождает идентификатор касания.</summary>
        void ResetInput() { pointer = -999; Value = Vector2.zero; if (handle) handle.anchoredPosition = Vector2.zero; }
    }
}
