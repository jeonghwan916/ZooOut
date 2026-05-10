using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 조준 입력 액션의 생명주기와 포인터 위치 읽기를 담당
public class PlayerAimInputHandler : MonoBehaviour, KnockoutInputActions.IPlayerActions
{
    private KnockoutInputActions _inputActions;

    public event Action PressStarted;

    public event Action PressCanceled;

    private void Awake()
    {
        _inputActions = new KnockoutInputActions();
        _inputActions.Player.SetCallbacks(this);
    }

    private void OnEnable()
    {
        if (_inputActions != null)
        {
            _inputActions.Enable();
        }
    }

    private void OnDisable()
    {
        if (_inputActions != null)
        {
            _inputActions.Disable();
        }
    }

    private void OnDestroy()
    {
        if (_inputActions == null)
        {
            return;
        }

        _inputActions.Player.RemoveCallbacks(this);
        _inputActions.Dispose();
        _inputActions = null;
    }

    public Vector2 ReadPointerPosition()
    {
        if (Pointer.current == null)
        {
            return Vector2.zero;
        }

        return Pointer.current.position.ReadValue();
    }

    public void OnAimPress(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            PressStarted?.Invoke();
            return;
        }

        if (context.canceled)
        {
            PressCanceled?.Invoke();
        }
    }
}
