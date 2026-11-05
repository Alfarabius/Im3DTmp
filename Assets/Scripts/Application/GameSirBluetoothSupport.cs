using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

public static class GameSirBluetoothSupport
{
    private const string LayoutName = "GameSirT4ProBluetooth";
    private static bool _registered;
    private static bool _deviceAdded;

    private static InputDeviceMatcher Matcher => new InputDeviceMatcher()
        .WithInterface("HID")
        .WithManufacturer("^ZhiXu Tech\\.$")
        .WithProduct("^GamePad$")
        .WithCapability("vendorId", 6473)
        .WithCapability("productId", 1026)
        .WithCapability("usagePage", 1)
        .WithCapability("usage", 5);

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_registered) return;
        InputSystem.onDeviceChange -= DeviceChanged;
        InputSystem.onAfterUpdate -= RegisterConnectedDevice;
        InputSystem.onDeviceChange += DeviceChanged;
        InputSystem.onAfterUpdate += RegisterConnectedDevice;
        _deviceAdded = true;
        RegisterConnectedDevice();
    }

    private static void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added && Matcher.MatchPercentage(device.description) > 0f)
            _deviceAdded = true;
    }

    private static void RegisterConnectedDevice()
    {
        if (_registered || !_deviceAdded) return;
        _deviceAdded = false;
        foreach (var device in InputSystem.devices)
        {
            if (!(device is Joystick) || Matcher.MatchPercentage(device.description) <= 0f) continue;
            var source = InputSystem.LoadLayout(device.layout);
            if (source == null) continue;
            // Смещения и нормализацию берём из раскладки, которую Unity прочитала из HID.
            // Номера кнопок: SDL GameControllerDB, Windows, VID 1949 / PID 0402.
            try
            {
                BuildLayout(source);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning("[Input] GameSir HID: " + exception.Message + ". Controls: " +
                    string.Join(", ", source.controls.Select(item => item.name.ToString())));
                return;
            }
            _registered = true;
            InputSystem.onDeviceChange -= DeviceChanged;
            InputSystem.onAfterUpdate -= RegisterConnectedDevice;
            InputSystem.RegisterLayoutBuilder(() => BuildLayout(source), LayoutName, "Gamepad", Matcher);
            return;
        }
    }

    public static InputControlLayout BuildLayout(InputControlLayout source)
    {
        var builder = new InputControlLayout.Builder()
            .WithName(LayoutName).WithType<Gamepad>().Extend("Gamepad")
            .WithFormat(source.stateFormat);

        foreach (var item in source.controls)
        {
            var control = builder.AddControl(item.name.ToString());
            if (!item.layout.IsEmpty()) control.WithLayout(item.layout.ToString());
            control.WithByteOffset(item.offset).WithBitOffset(item.bit).WithSizeInBits(item.sizeInBits);
            if (item.format != default) control.WithFormat(item.format);
            control.UsingStateFrom(item.useStateFrom);
            CopyParameters(control, item);
        }

        Alias(builder, source, "leftStick", "stick");
        foreach (string child in new[] { "x", "y", "up", "down", "left", "right" })
            Alias(builder, source, "leftStick/" + child, "stick/" + child);

        var axes = RightStickAxes(source);
        var x = axes[0];
        var y = axes[1];
        string xName = x.name.ToString();
        string yName = y.name.ToString();
        uint start = Math.Min(x.offset, y.offset);
        uint end = Math.Max(x.offset * 8 + x.bit + x.sizeInBits, y.offset * 8 + y.bit + y.sizeInBits);
        builder.AddControl("rightStick").WithByteOffset(start).WithSizeInBits(end - start * 8);
        Alias(builder, source, "rightStick/x", xName, start);
        string yParameters = string.Join(",", y.parameters.Where(value => value.name != "invert").Select(value => value.ToString()));
        Alias(builder, source, "rightStick/y", yName, start)
            .WithParameters((string.IsNullOrEmpty(yParameters) ? "" : yParameters + ",") + "invert=true");
        Direction(builder, source, "up", yName, "clampMin=-1,clampMax=0,invert=true");
        Direction(builder, source, "down", yName, "clampMin=0,clampMax=1,invert=false");
        Direction(builder, source, "left", xName, "clampMin=-1,clampMax=0,invert=true");
        Direction(builder, source, "right", xName, "clampMin=0,clampMax=1,invert=false");

        Alias(builder, source, "dpad", "hat");
        foreach (string child in new[] { "up", "down", "left", "right" })
            Alias(builder, source, "dpad/" + child, "hat/" + child);

        Alias(builder, source, "buttonSouth", "trigger");
        Alias(builder, source, "buttonEast", "button2");
        Alias(builder, source, "buttonWest", "button4");
        Alias(builder, source, "buttonNorth", "button5");
        Alias(builder, source, "leftShoulder", "button7");
        Alias(builder, source, "rightShoulder", "button8");
        Alias(builder, source, "leftTrigger", "button9");
        Alias(builder, source, "rightTrigger", "button10");
        Alias(builder, source, "select", "button11");
        Alias(builder, source, "start", "button12");
        Alias(builder, source, "leftStickPress", "button14");
        Alias(builder, source, "rightStickPress", "button15");
        return builder.Build();
    }

    private static InputControlLayout.ControlItem Find(InputControlLayout source, string name)
    {
        foreach (var item in source.controls)
            if (item.name == name) return item;
        throw new InvalidOperationException("GameSir HID control is missing: " + name);
    }

    private static InputControlLayout.ControlItem[] RightStickAxes(InputControlLayout source)
    {
        var axes = source.controls.Where(item => item.layout == "Axis" && !item.name.ToString().Contains("/") &&
                item.offset != InputStateBlock.InvalidOffset && item.sizeInBits > 0 && item.format != default)
            .OrderBy(item => item.offset).ThenBy(item => item.bit).ToArray();
        foreach (var pair in new[] { new[] { "rx", "ry" }, new[] { "z", "rz" }, new[] { "rotateX", "rotateY" },
                     new[] { "rotationX", "rotationY" }, new[] { "z", "rotateZ" } })
        {
            var x = Array.FindIndex(axes, item => item.name == pair[0]);
            var y = Array.FindIndex(axes, item => item.name == pair[1]);
            if (x >= 0 && y >= 0) return new[] { axes[x], axes[y] };
        }
        if (axes.Length == 2) return axes;
        throw new InvalidOperationException("Cannot identify right stick axes: " +
            string.Join(", ", axes.Select(item => item.name.ToString())));
    }

    private static InputControlLayout.Builder.ControlBuilder Alias(InputControlLayout.Builder builder,
        InputControlLayout source, string target, string origin,
        uint parentOffset = 0)
    {
        var item = Find(source, origin);
        var control = builder.AddControl(target);
        if (target.Contains("/"))
        {
            // Смещения дочерних контролов задаются относительно их стика/крестовины.
            control.WithByteOffset(item.offset == InputStateBlock.InvalidOffset ? item.offset : item.offset - parentOffset)
                .WithBitOffset(item.bit).WithSizeInBits(item.sizeInBits);
            if (item.format != default) control.WithFormat(item.format);
        }
        else control.UsingStateFrom(origin);
        if (!item.layout.IsEmpty()) control.WithLayout(item.layout.ToString());
        CopyParameters(control, item);
        return control;
    }

    private static void Direction(InputControlLayout.Builder builder, InputControlLayout source, string direction,
        string axis, string parameters)
    {
        var item = Find(source, axis);
        string original = string.Join(",", item.parameters
            .Where(value => value.name != "invert" && value.name != "clamp" && value.name != "clampMin" && value.name != "clampMax")
            .Select(value => value.ToString()));
        builder.AddControl("rightStick/" + direction)
            .WithParameters((string.IsNullOrEmpty(original) ? "" : original + ",") + "clamp=2," + parameters);
    }

    private static string Parameters(InputControlLayout.ControlItem item) =>
        string.Join(",", item.parameters.Select(value => value.ToString()));

    private static void CopyParameters(InputControlLayout.Builder.ControlBuilder control, InputControlLayout.ControlItem item)
    {
        control.WithParameters(Parameters(item));
        control.WithProcessors(string.Join(",", item.processors.Select(value => value.ToString())));
        if (!item.defaultState.isEmpty) control.WithDefaultState(item.defaultState);
    }
}
