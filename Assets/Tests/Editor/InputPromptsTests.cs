using System.Reflection;
using NUnit.Framework;
using UnityEngine.InputSystem;

public class InputPromptsTests
{
    [SetUp]
    public void UseKeyboard() => SetGamepad(false);

    [TearDown]
    public void RestoreKeyboard() => SetGamepad(false);

    private static void SetGamepad(bool value) => typeof(GameInput)
        .GetMethod("SetGamepad", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { value });

    [Test]
    public void InteractionPromptUsesPhysicalFAndSwitchesToXboxX()
    {
        using (var action = new InputAction("Interact", InputActionType.Button))
        {
            action.AddBinding("<Keyboard>/f");
            action.AddBinding("<Gamepad>/buttonWest");
            Assert.AreEqual("F", GameInput.GetButtonLabel(action));
            SetGamepad(true);
            Assert.AreEqual("X", GameInput.GetButtonLabel(action));
            SetGamepad(false);
            Assert.AreEqual("F", GameInput.GetButtonLabel(action));
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
