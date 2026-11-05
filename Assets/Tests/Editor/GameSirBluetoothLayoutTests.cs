using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

public class GameSirBluetoothLayoutTests
{
    private static InputControlLayout NativeLayout(string rightX = "rx", string rightY = "ry", uint offset = 4,
        bool invertRightY = true)
    {
        var b = new InputControlLayout.Builder().WithName("NativeHID").WithFormat("HID");
        b.AddControl("stick").WithLayout("Stick").WithByteOffset(1).WithBitOffset(0).WithSizeInBits(16);
        foreach (string child in new[] { "x", "y", "up", "down", "left", "right" })
            b.AddControl("stick/" + child).WithLayout(child == "x" || child == "y" ? "Axis" : "Button")
                .WithFormat("BYTE").WithByteOffset(0).WithBitOffset(0).WithSizeInBits(8)
                .WithParameters("normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5");
        b.AddControl(rightX).WithLayout("Axis").WithFormat("BYTE").WithByteOffset(offset).WithBitOffset(0).WithSizeInBits(8)
            .WithParameters("normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5");
        b.AddControl(rightY).WithLayout("Axis").WithFormat("BYTE").WithByteOffset(offset + 1).WithBitOffset(0).WithSizeInBits(8)
            .WithParameters("normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,invert=" + (invertRightY ? "true" : "false"));
        b.AddControl("hat").WithLayout("Dpad").WithByteOffset(7).WithBitOffset(0).WithSizeInBits(4);
        foreach (string child in new[] { "up", "down", "left", "right" })
            b.AddControl("hat/" + child).WithLayout("DiscreteButton").WithFormat("BIT").WithSizeInBits(4)
                .WithParameters("minValue=1,maxValue=3");
        b.AddControl("trigger").WithLayout("Button").WithFormat("BIT").WithByteOffset(8).WithBitOffset(0).WithSizeInBits(1);
        foreach (int button in new[] { 2, 4, 5, 7, 8, 9, 10, 11, 12, 14, 15 })
            b.AddControl("button" + button).WithLayout("Button").WithFormat("BIT")
                .WithByteOffset(8).WithBitOffset((uint)(button - 1)).WithSizeInBits(1);
        return b.Build();
    }

    private static InputControlLayout.ControlItem Control(InputControlLayout layout, string name)
    {
        foreach (var control in layout.controls)
            if (control.name == name) return control;
        throw new Exception("Missing control: " + name);
    }

    [Test]
    public void NativeStateIsExposedThroughXboxControls()
    {
        var source = NativeLayout();
        var layout = GameSirBluetoothSupport.BuildLayout(source);
        Assert.AreEqual(typeof(Gamepad), layout.type);
        Assert.AreEqual(source.stateFormat, layout.stateFormat);
        Assert.AreEqual("stick", Control(layout, "leftStick").useStateFrom);
        Assert.AreEqual("button4", Control(layout, "buttonWest").useStateFrom);
        Assert.AreEqual("button10", Control(layout, "rightTrigger").useStateFrom);
        Assert.AreEqual("button12", Control(layout, "start").useStateFrom);
        Assert.AreEqual("button14", Control(layout, "leftStickPress").useStateFrom);
        Assert.AreEqual(Control(source, "button10").offset, Control(layout, "button10").offset);
        Assert.AreEqual(Control(source, "button10").bit, Control(layout, "button10").bit);
    }

    [Test]
    public void StickAndDpadKeepHidDecodingAndCorrectStateRanges()
    {
        var layout = GameSirBluetoothSupport.BuildLayout(NativeLayout());
        Assert.AreEqual(4, Control(layout, "rightStick").offset);
        Assert.AreEqual(16, Control(layout, "rightStick").sizeInBits);
        Assert.AreEqual(0, Control(layout, "rightStick/x").offset);
        Assert.AreEqual(1, Control(layout, "rightStick/y").offset);
        Assert.IsNull(Control(layout, "rightStick/x").useStateFrom);
        Assert.AreEqual("DiscreteButton", Control(layout, "dpad/up").layout.ToString());
        Assert.IsNotEmpty(Control(layout, "rightStick/y").parameters);
        Assert.IsNotEmpty(Control(layout, "dpad/up").parameters);
        var up = Control(layout, "rightStick/up").parameters.Where(value => value.name == "invert").ToArray();
        var down = Control(layout, "rightStick/down").parameters.Where(value => value.name == "invert").ToArray();
        Assert.AreEqual(1, up.Length);
        Assert.AreEqual(1, down.Length);
        Assert.IsTrue(up[0].value.ToBoolean());
        Assert.IsFalse(down[0].value.ToBoolean());
    }

    [Test]
    public void IncompleteHidLayoutIsRejected()
    {
        var source = new InputControlLayout.Builder().WithName("Incomplete").WithFormat("HID").Build();
        Assert.Throws<InvalidOperationException>(() => GameSirBluetoothSupport.BuildLayout(source));
    }

    [Test]
    public void MissingRxRyDoesNotPreventMappingOtherNativeAxisNames()
    {
        foreach (var names in new[] { new[] { "z", "rz" }, new[] { "rotateX", "rotateY" },
                     new[] { "vendorHorizontal", "vendorVertical" } })
        {
            var layout = GameSirBluetoothSupport.BuildLayout(NativeLayout(names[0], names[1], 3, false));
            Assert.AreEqual(3, Control(layout, "rightStick").offset);
            Assert.AreEqual(0, Control(layout, "rightStick/x").offset);
            Assert.AreEqual(1, Control(layout, "rightStick/y").offset);
            Assert.AreEqual("button4", Control(layout, "buttonWest").useStateFrom);
            Assert.IsTrue(Control(layout, "rightStick/y").parameters.Single(value => value.name == "invert").value.ToBoolean());
        }
    }

    [Test]
    public void DeviceMatcherOutranksGenericHidWithoutMatchingOtherControllers()
    {
        var description = new InputDeviceDescription
        {
            interfaceName = "HID", manufacturer = "ZhiXu Tech.", product = "GamePad", version = "0",
            capabilities = "{\"vendorId\":6473,\"productId\":1026,\"usage\":5,\"usagePage\":1,\"inputReportSize\":11}"
        };
        var matcher = (InputDeviceMatcher)typeof(GameSirBluetoothSupport)
            .GetProperty("Matcher", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var generic = InputDeviceMatcher.FromDeviceDescription(description)
            .WithCapability("usage", 5).WithCapability("usagePage", 1);
        Assert.Greater(matcher.MatchPercentage(description), generic.MatchPercentage(description));
        description.manufacturer = "Other";
        Assert.AreEqual(0f, matcher.MatchPercentage(description));
    }
}
