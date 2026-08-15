using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputController
{
    public Vector2 Move { get; private set; }
    public Vector2 PointerScreenPosition => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
    public bool DodgePressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    public bool AttackPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public bool UltimatePressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    public bool CancelPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    public bool PointerPressed => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    public bool PointerHeld => Pointer.current != null && Pointer.current.press.isPressed;
    public bool PointerReleased => Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
    public void Tick()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) { Move = Vector2.zero; return; }
        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }
}
