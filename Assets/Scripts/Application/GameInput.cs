using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GameInput
{
    public static bool UsingGamepad { get; private set; }
    public static event Action ControlsChanged;
    private static InputDevice _lastDevice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        UsingGamepad = false;
        _lastDevice = null;
        ControlsChanged = null;
        InputSystem.onActionChange -= OnActionChange;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        InputSystem.onActionChange += OnActionChange;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private static void OnActionChange(object value, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed || !(value is InputAction action)) return;
        var device = action.activeControl?.device;
        if (!(device is Gamepad) && !(device is Keyboard) && !(device is Mouse)) return;
        _lastDevice = device;
        SetGamepad(device is Gamepad);
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device == _lastDevice && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected))
        {
            _lastDevice = null;
            SetGamepad(false);
        }
    }

    private static void SetGamepad(bool gamepad)
    {
        if (UsingGamepad == gamepad) return;
        UsingGamepad = gamepad;
        ControlsChanged?.Invoke();
    }

    public static string GetButtonLabel(InputAction action)
    {
        if (action == null) return string.Empty;
        string fallback = null;
        foreach (var binding in action.bindings)
        {
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
            string path = binding.effectivePath;
            if (fallback == null) fallback = path;
            bool gamepad = path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
            if (gamepad == UsingGamepad) return GetBindingLabel(path);
        }
        return GetBindingLabel(fallback);
    }

    public static string GetBindingLabel(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        string control = path.Substring(path.LastIndexOf('/') + 1);
        // Название физической клавиши не зависит от языка раскладки ОС.
        if (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
        {
            switch (control)
            {
                case "space": return "Space";
                case "escape": return "Esc";
                case "leftShift": case "rightShift": return "Shift";
                case "leftCtrl": case "rightCtrl": return "Ctrl";
                case "leftAlt": case "rightAlt": return "Alt";
                case "upArrow": return "Up";
                case "downArrow": return "Down";
                case "leftArrow": return "Left";
                case "rightArrow": return "Right";
                default: return control.ToUpperInvariant();
            }
        }
        switch (control)
        {
            case "buttonSouth": return "A";
            case "buttonEast": return "B";
            case "buttonWest": return "X";
            case "buttonNorth": return "Y";
            case "leftShoulder": return "LB";
            case "rightShoulder": return "RB";
            case "leftTrigger": return "LT";
            case "rightTrigger": return "RT";
            case "leftStickPress": return "LS";
            case "rightStickPress": return "RS";
            case "start": return "Menu";
            case "leftButton": return "LMB";
            case "rightButton": return "RMB";
            default: return control;
        }
    }
}
