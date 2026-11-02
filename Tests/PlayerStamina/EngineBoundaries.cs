// Engine adapters for headless checks, not a rendering/audio simulation.
namespace UnityEngine
{
    public class CharacterController : Component {}
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
}
namespace UnityEngine.InputSystem { public class InputActionReference {} }
namespace Unity.Cinemachine
{
    public class CinemachineBasicMultiChannelPerlin : UnityEngine.Component
    {
        public float AmplitudeGain=1,FrequencyGain=1;
    }
}
namespace UnityEngine.Rendering.Universal
{
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
        public bool TryGet<T>(out T component) where T:class { component=Chromatic as T; return component!=null; }
        public VolumeProfile Clone() => new VolumeProfile { Chromatic=new ChromaticAberration { active=Chromatic.active, intensity=new FloatParameter { value=Chromatic.intensity.value,overrideState=Chromatic.intensity.overrideState } } };
    }
    public class Volume : UnityEngine.Component
    {
        public VolumeProfile sharedProfile=new VolumeProfile();
        VolumeProfile instance;
        public VolumeProfile profile => instance ?? (instance=sharedProfile.Clone());
    }
}
public static class PauseManager { public static bool IsPaused; }
// Player state transitions are outside this test's scope; real PlayerBrain is compiled below.
public class PlayerActiveState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
public class PlayerHeavyWoundedState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
public class PlayerDeadState : UnityEngine.MonoBehaviour,ICharacterState { public void Enter() {} public void Exit() {} public void UpdateState() {} }
