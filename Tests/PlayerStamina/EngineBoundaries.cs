// Engine adapters for headless checks, not a rendering/audio simulation.
namespace UnityEngine
{
    public struct Color
    {
        public float r,g,b,a;
        public static Color red => new Color { r=1,a=1 };
        public static Color white => new Color { r=1,g=1,b=1,a=1 };
        public static Color yellow => new Color { r=1,g=1,a=1 };
        public static Color black => new Color { a=1 };
        public static Color operator *(Color c,float scale) => new Color { r=c.r*scale,g=c.g*scale,b=c.b*scale,a=c.a*scale };
        public static Color Lerp(Color from,Color to,float t) => new Color
        {
            r=Mathf.Lerp(from.r,to.r,t),g=Mathf.Lerp(from.g,to.g,t),b=Mathf.Lerp(from.b,to.b,t),a=Mathf.Lerp(from.a,to.a,t)
        };
    }
    public class MaterialPropertyBlock { public void SetColor(string name,Color color) {} }
    public class MeshRenderer : Component { public void GetPropertyBlock(MaterialPropertyBlock block) {} public void SetPropertyBlock(MaterialPropertyBlock block) {} }
    public class CharacterController : Component {}
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
}
namespace UnityEngine.InputSystem
{
    public class InputActionReference { public InputAction action=new InputAction(); }
    public class InputAction { public bool WasPressedThisFrame() => false; }
}
namespace Unity.Cinemachine
{
    public class CinemachineImpulseSource : UnityEngine.Component { public int Impulses; public void GenerateImpulse() { Impulses++; } }
    public class CinemachineBasicMultiChannelPerlin : UnityEngine.Component
    {
        public float AmplitudeGain=1,FrequencyGain=1;
    }
}
namespace UnityEngine.Rendering.Universal
{
    public class ColorParameter { public UnityEngine.Color value; }
    public class Vignette : UnityEngine.Object { public FloatParameter intensity=new FloatParameter(); public ColorParameter color=new ColorParameter(); }
    public class ColorAdjustments : UnityEngine.Object { public FloatParameter saturation=new FloatParameter(); }
    public class FloatParameter { public float value; public bool overrideState; }
    public class ChromaticAberration : UnityEngine.Object
    {
        public bool active=true;
        public FloatParameter intensity = new FloatParameter();
    }
}
namespace UnityEngine.Rendering
{
    using UnityEngine.Rendering.Universal;
    public class VolumeProfile
    {
        public ChromaticAberration Chromatic = new ChromaticAberration();
        public Vignette Vignette = new Vignette();
        public ColorAdjustments Color = new ColorAdjustments();
        public bool TryGet<T>(out T component) where T:class { component=Chromatic as T ?? Vignette as T ?? Color as T; return component!=null; }
        public VolumeProfile Clone() => new VolumeProfile
        {
            Chromatic=new ChromaticAberration { active=Chromatic.active, intensity=new FloatParameter { value=Chromatic.intensity.value,overrideState=Chromatic.intensity.overrideState } },
            Vignette=new Vignette { intensity=new FloatParameter { value=Vignette.intensity.value,overrideState=Vignette.intensity.overrideState },color=new ColorParameter { value=Vignette.color.value } },
            Color=new ColorAdjustments { saturation=new FloatParameter { value=Color.saturation.value,overrideState=Color.saturation.overrideState } }
        };
    }
    public class Volume : UnityEngine.Component
    {
        public VolumeProfile sharedProfile=new VolumeProfile();
        VolumeProfile instance;
        public VolumeProfile profile => instance ?? (instance=sharedProfile.Clone());
    }
}
namespace UnityEngine.Audio { public class AudioMixerGroup : UnityEngine.Object {} }
public static class PauseManager { public static bool IsPaused; }
// Player state transitions are outside this test's scope; real PlayerBrain is compiled below.
public class PlayerActiveState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
public class PlayerHeavyWoundedState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
public class PlayerDeadState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
