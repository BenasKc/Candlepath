using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Minimal pointer hooks; avoids Button's built-in tint transitions. The scripts set the actions.
public class UiPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onEnter, onExit, onClick;
    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }
}
