using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, Input_Actions.IPlayerActions
{
    // Drag events
    public event Action<Vector2> OnDragStarted;   // screen-space pos at press start
    public event Action<Vector2> OnDrag;          // screen-space pos while holding
    public event Action<Vector2> OnDragEnded;     // screen-space pos at release

    // Public state
    public Vector2 LastPointerScreenPosition { get; private set; }
    public bool IsPointerHeld { get; private set; }

    // Internals
    public Input_Actions InputActions { get; private set; }
    private readonly Dictionary<InputAction, Coroutine> activeDisableCoroutines = new();

    public void EnableInput()
    {
        if (InputActions == null)
        {
            InputActions = new Input_Actions();
            InputActions.Player.SetCallbacks(this); // hook generated callbacks
        }
        InputActions.Enable();
    }

    public void DisableInput()
    {
        if (InputActions == null) return;
        InputActions.Disable();
    }


    public void OnPointerPosition(InputAction.CallbackContext context)
    {
        LastPointerScreenPosition = context.ReadValue<Vector2>();
        if (IsPointerHeld) OnDrag?.Invoke(LastPointerScreenPosition);
    }

    public void OnPointerPress(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsPointerHeld = true;
            OnDragStarted?.Invoke(LastPointerScreenPosition);
        }
        else if (context.canceled)
        {
            IsPointerHeld = false;
            OnDragEnded?.Invoke(LastPointerScreenPosition);
        }
    }

    // Temporarily disable an action
    public void DisableActionFor(InputAction action, float seconds, MonoBehaviour caller)
    {
        if (action == null || caller == null) return;

        if (activeDisableCoroutines.ContainsKey(action))
            caller.StopCoroutine(activeDisableCoroutines[action]);

        Coroutine routine = caller.StartCoroutine(DisableActionCoroutine(action, seconds));
        activeDisableCoroutines[action] = routine;
    }

    private IEnumerator DisableActionCoroutine(InputAction action, float seconds)
    {
        action.Disable();
        yield return new WaitForSeconds(seconds);
        action.Enable();
        activeDisableCoroutines.Remove(action);
    }
}
