using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Routes desktop pointer events through the aim point while a gameplay crosshair is active.</summary>
public sealed class UttCenteredPointer : PointerInputModule
{
    int readyFrame = -1;
    PointerEventData lastHit;
    readonly bool[] blockedButtons = new bool[3];
    internal EventSystem Owner => eventSystem;

    internal bool HasGraphicAtCenter()
    {
        RefreshHit();
        return lastHit.pointerCurrentRaycast.gameObject != null;
    }

    void RefreshHit()
    {
        if (readyFrame == Time.frameCount) return;
        readyFrame = Time.frameCount;
        lastHit ??= new PointerEventData(eventSystem);
        lastHit.Reset();
        lastHit.position = new Vector2(Screen.width * .5f, Screen.height * .5f);
        eventSystem.RaycastAll(lastHit, m_RaycastResultCache);
        lastHit.pointerCurrentRaycast = default;
        foreach (var hit in m_RaycastResultCache)
        {
            if (hit.module is not GraphicRaycaster || !SupportsPointer(hit.gameObject)) continue;
            lastHit.pointerCurrentRaycast = hit;
            break;
        }
        m_RaycastResultCache.Clear();
    }

    static bool SupportsPointer(GameObject target) =>
        ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target) != null ||
        ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null ||
        ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
        ExecuteEvents.GetEventHandler<IScrollHandler>(target) != null ||
        ExecuteEvents.GetEventHandler<IDragHandler>(target) != null ||
        ExecuteEvents.GetEventHandler<IPointerUpHandler>(target) != null;

    public override void ActivateModule()
    {
        base.ActivateModule();
        var mouse = Mouse.current;
        blockedButtons[0] = mouse?.leftButton.isPressed == true;
        blockedButtons[1] = mouse?.rightButton.isPressed == true;
        blockedButtons[2] = mouse?.middleButton.isPressed == true;
    }

    public override void DeactivateModule()
    {
        foreach (var data in m_PointerData.Values)
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
        ClearSelection();
        readyFrame = -1;
        base.DeactivateModule();
    }

    public override void Process()
    {
        var mouse = Mouse.current;
        if (mouse == null || !UttDesktopCursor.CanInteract) return;
        RefreshHit();
        var left = Route(-1, PointerEventData.InputButton.Left, mouse.leftButton, 0);
        Route(-2, PointerEventData.InputButton.Right, mouse.rightButton, 1);
        Route(-3, PointerEventData.InputButton.Middle, mouse.middleButton, 2);
        left.scrollDelta = mouse.scroll.ReadValue();
        if (left.scrollDelta != Vector2.zero)
            ExecuteEvents.ExecuteHierarchy(left.pointerCurrentRaycast.gameObject, left, ExecuteEvents.scrollHandler);
    }

    PointerEventData Route(int id, PointerEventData.InputButton button,
        UnityEngine.InputSystem.Controls.ButtonControl control, int slot)
    {
        GetPointerData(id, out var data, true);
        data.Reset();
        data.position = lastHit.position;
        data.delta = Vector2.zero;
        data.button = button;
        data.pointerCurrentRaycast = lastHit.pointerCurrentRaycast;
        var target = data.pointerCurrentRaycast.gameObject;
        if (slot == 0) ProcessMove(data);
        if (blockedButtons[slot])
        {
            if (!control.isPressed) blockedButtons[slot] = false;
            return data;
        }

        if (control.wasPressedThisFrame)
        {
            DeselectIfSelectionChanged(target, data);
            data.eligibleForClick = true;
            data.pressPosition = data.position;
            data.pointerPressRaycast = data.pointerCurrentRaycast;
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            var press = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler) ?? click;
            float now = Time.unscaledTime;
            data.clickCount = press != null && press == data.lastPress && now - data.clickTime < .3f ? data.clickCount + 1 : 1;
            data.clickTime = now;
            data.pointerPress = press;
            data.rawPointerPress = target;
        }

        if (control.wasReleasedThisFrame)
        {
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (data.eligibleForClick && data.pointerPress == ExecuteEvents.GetEventHandler<IPointerClickHandler>(target))
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            data.eligibleForClick = false;
            data.pointerPress = null;
            data.rawPointerPress = null;
        }
        return data;
    }
}
