using System.Reflection;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

public class InputPromptsTests
{
    [SetUp]
    public void UseKeyboard() => SetGamepad(false);

    [TearDown]
    public void RestoreKeyboard() => SetGamepad(false);

    private static void SetGamepad(bool value) => typeof(GameInput)
        .GetMethod("SetGamepad", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { value });

    private static bool AnyButtonPressed(InputDevice device) => (bool)typeof(GameInput)
        .GetMethod("AnyButtonPressed", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(InputDevice) }, null)
        .Invoke(null, new object[] { device });

    [TestCase(Key.W)]
    [TestCase(Key.Space)]
    [TestCase(Key.Escape)]
    public void AnyKeyCanBeHeldForCutsceneSkip(Key key)
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            Assert.IsFalse(AnyButtonPressed(keyboard));
            InputState.Change(keyboard[key], 1f);
            Assert.IsTrue(AnyButtonPressed(keyboard));
            Assert.IsTrue(AnyButtonPressed(keyboard));
            InputState.Change(keyboard[key], 0f);
            Assert.IsFalse(AnyButtonPressed(keyboard));
        }
        finally { InputSystem.RemoveDevice(keyboard); }
    }

    [Test]
    public void MouseMotionAndScrollingDoNotSkipButRightButtonDoes()
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            InputState.Change(mouse.delta, new Vector2(30, 20));
            InputState.Change(mouse.scroll, new Vector2(0, 120));
            Assert.IsFalse(AnyButtonPressed(mouse));
            InputState.Change(mouse.rightButton, 1f);
            Assert.IsTrue(AnyButtonPressed(mouse));
            InputState.Change(mouse.rightButton, 0f);
            Assert.IsFalse(AnyButtonPressed(mouse));
        }
        finally { InputSystem.RemoveDevice(mouse); }
    }

    [Test]
    public void GamepadButtonsAndTriggersSkipButStickMotionDoesNot()
    {
        var gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            InputState.Change(gamepad.leftStick, Vector2.right);
            InputState.Change(gamepad.rightStick, Vector2.up);
            Assert.IsFalse(AnyButtonPressed(gamepad));
            foreach (var button in new[] { gamepad.buttonEast, gamepad.dpad.up, gamepad.leftStickButton, gamepad.rightTrigger })
            {
                InputState.Change(button, 1f);
                Assert.IsTrue(AnyButtonPressed(gamepad));
                InputState.Change(button, 0f);
                Assert.IsFalse(AnyButtonPressed(gamepad));
            }
        }
        finally { InputSystem.RemoveDevice(gamepad); }
    }

    [Test]
    public void RawJoystickButtonCanSkip()
    {
        var joystick = InputSystem.AddDevice<Joystick>();
        try
        {
            InputState.Change(joystick.stick, Vector2.one);
            Assert.IsFalse(AnyButtonPressed(joystick));
            InputState.Change(joystick.trigger, 1f);
            Assert.IsTrue(AnyButtonPressed(joystick));
        }
        finally { InputSystem.RemoveDevice(joystick); }
    }

    [Test]
    public void InteractionPromptShowsBothKeysAndSwitchesToXboxX()
    {
        using (var action = new InputAction("Interact", InputActionType.Button))
        {
            action.AddBinding("<Keyboard>/f");
            action.AddBinding("<Keyboard>/e");
            action.AddBinding("<Gamepad>/buttonWest");
            Assert.AreEqual("F/E", GameInput.GetButtonLabel(action));
            SetGamepad(true);
            Assert.AreEqual("X", GameInput.GetButtonLabel(action));
            SetGamepad(false);
            Assert.AreEqual("F/E", GameInput.GetButtonLabel(action));
        }
    }

    [Test]
    public void PromptRespectsBindingOverride()
    {
        using (var action = new InputAction("Interact", binding: "<Keyboard>/f"))
        {
            action.ApplyBindingOverride("<Keyboard>/g");
            Assert.AreEqual("G", GameInput.GetButtonLabel(action));
        }
    }

    [Test]
    public void PromptSkipsCompositePartsAndDisabledBindings()
    {
        using (var action = new InputAction("Interact"))
        {
            action.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            action.AddBinding("<Keyboard>/e");
            action.AddBinding("<Keyboard>/f");
            action.ApplyBindingOverride(3, new InputBinding { overridePath = string.Empty });
            Assert.AreEqual("F", GameInput.GetButtonLabel(action));
        }
    }
}
