using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager instance { get; private set; }
    private InputSystem inputSystem;

    public delegate void OnPlayerMovementInput(Vector2 movementInput);
    public OnPlayerMovementInput onPlayerMovementInput;

    public Action onBackPressed;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        inputSystem = new InputSystem();
    }

    private void OnEnable()
    {
        inputSystem.Enable();

        inputSystem.Player.Move.performed += ctx => onPlayerMovementInput(ctx.ReadValue<Vector2>());
        inputSystem.Player.Back.performed += ctx => onBackPressed?.Invoke();
    }

    private void OnDisable()
    {
        inputSystem.Disable();

        inputSystem.Player.Move.performed -= ctx => onPlayerMovementInput(ctx.ReadValue<Vector2>());
        inputSystem.Player.Back.performed -= ctx => onBackPressed?.Invoke();
    }

    public Vector2 GetPlayerMovement()
    {
        return inputSystem.Player.Move.ReadValue<Vector2>();
    }
}
