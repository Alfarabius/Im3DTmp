using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

class Fixture : IDisposable
{
    public PlayerBrain Brain;
    public PlayerStaminaEffects Effects;
    public AudioSource Audio;
    public CinemachineBasicMultiChannelPerlin Noise;
    public Volume Volume;
    public ChromaticAberration Chromatic => Volume.profile.Chromatic;
    public List<bool> Events=new List<bool>();
    public static void Set(object target,string name,object value)
    {
        var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        if(field!=null) field.SetValue(target,value);
        else target.GetType().GetProperty(name).SetValue(target,value);
    }
    public static void Call(object target,string name) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    public Fixture(bool audio=true,bool visuals=true)
    {
        PauseManager.IsPaused=false;
        var player=new GameObject(); Brain=player.AddComponent<PlayerBrain>();
        Set(Brain,"Anim",player.AddComponent<Animator>());
        Audio=player.AddComponent<AudioSource>(); Audio.clip=new AudioClip();
        Noise=player.AddComponent<CinemachineBasicMultiChannelPerlin>();
        Volume=player.AddComponent<Volume>(); Volume.sharedProfile.Chromatic.intensity.value=0.128f; Volume.sharedProfile.Chromatic.intensity.overrideState=true;
        Effects=player.AddComponent<PlayerStaminaEffects>();
        if(audio) Set(Effects,"breathingAudio",Audio);
        if(visuals) { Set(Effects,"cameraNoise",Noise); Set(Effects,"postProcessVolume",Volume); }
        Call(Brain,"Awake"); Brain.OnStaminaDepletedChanged+=Events.Add;
        Call(Effects,"Awake"); Call(Effects,"OnEnable");
    }
    public void Tick(float seconds=0.1f,int count=1) { Time.deltaTime=seconds; for(int i=0;i<count;i++) Call(Effects,"LateUpdate"); }
    public void Exhaust() { Brain.CurrentStamina=0; Tick(0.1f,8); }
    public void Dispose() { Call(Effects,"OnDisable"); }
}
class Program
{
    static int count;
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void Near(float a,float b,string message) { Assert(Math.Abs(a-b)<0.0001f,message+": "+a+" != "+b); }
    static void Run(string name,Action<Fixture> test)
    {
        using(var f=new Fixture()) test(f);
        count++; Console.WriteLine("PASS "+name);
    }
    static int Main()
    {
        Run("healthy player has no exhaustion effects",f=> { f.Tick(); Assert(!f.Brain.IsStaminaDepleted && !f.Audio.isPlaying && f.Events.Count==0,"unexpected exhaustion"); Near(f.Noise.AmplitudeGain,1,"base noise"); Near(f.Chromatic.intensity.value,0.128f,"base chromatic"); });
        Run("low positive stamina does not trigger breathing",f=> { f.Brain.CurrentStamina=5; f.Tick(); Assert(!f.Brain.IsStaminaDepleted && !f.Audio.isPlaying && f.Events.Count==0,"triggered before zero"); });
        Run("zero emits one event; repeated zero does not restart clip",f=> { f.Brain.CurrentStamina=0; f.Tick(); f.Brain.CurrentStamina=0; f.Tick(); Assert(f.Events.Count==1 && f.Events[0] && f.Audio.Plays==1,"duplicate depletion"); });
        Run("negative stamina triggers without altering movement's value",f=> { f.Brain.CurrentStamina=-0.25f; Assert(f.Brain.IsStaminaDepleted,"negative crossing missed"); Near(f.Brain.CurrentStamina,-0.25f,"setter clamped existing movement"); });
        Run("onset is gradual for audio, noise and aberration",f=> { f.Brain.CurrentStamina=0; f.Tick(0.1f); Assert(f.Audio.volume>0 && f.Audio.volume<0.7f,"audio jumped"); Assert(f.Noise.AmplitudeGain>1 && f.Noise.AmplitudeGain<2,"noise jumped"); Assert(f.Chromatic.intensity.value>0.128f && f.Chromatic.intensity.value<0.478f,"chromatic jumped"); });
        Run("peak reached after configured fade-in duration",f=> { f.Exhaust(); Near(f.Audio.volume,0.7f,"audio peak"); Near(f.Noise.AmplitudeGain,2,"amplitude peak"); Near(f.Noise.FrequencyGain,1.25f,"frequency peak"); Near(f.Chromatic.intensity.value,0.478f,"chromatic peak"); Assert(f.Audio.loop && f.Audio.spatialBlend==0 && !f.Audio.playOnAwake,"breathing source setup"); });
        Run("all effects share the same smoothed envelope",f=> { f.Brain.CurrentStamina=0; f.Tick(0.2f); float strength=f.Audio.volume/0.7f; Near(f.Noise.AmplitudeGain-1,strength,"noise envelope"); Near((f.Chromatic.intensity.value-0.128f)/0.35f,strength,"chromatic envelope"); Near(strength,0.15625f,"smoothstep curve"); });
        Run("stamina exactly 15 keeps exhaustion active",f=> { f.Exhaust(); f.Brain.CurrentStamina=15; f.Tick(); Assert(f.Brain.IsStaminaDepleted && f.Audio.isPlaying && f.Events.Count==1,"recovered at 15"); Near(f.Audio.volume,0.7f,"audio changed at 15"); });
        Run("strictly above 15 emits recovery",f=> { f.Exhaust(); f.Brain.CurrentStamina=15.001f; Assert(!f.Brain.IsStaminaDepleted && f.Events.Count==2 && !f.Events[1],"strict threshold"); });
        Run("recovery fades all effects instead of snapping",f=> { f.Exhaust(); f.Brain.CurrentStamina=16; f.Tick(0.2f); Assert(f.Audio.volume>0 && f.Audio.volume<0.7f && f.Audio.isPlaying,"audio did not fade"); Assert(f.Noise.AmplitudeGain>1 && f.Noise.AmplitudeGain<2,"shake did not fade"); Assert(f.Chromatic.intensity.value>0.128f && f.Chromatic.intensity.value<0.478f,"aberration did not fade"); });
        Run("full fade-out stops breathing and restores baseline",f=> { f.Exhaust(); f.Brain.CurrentStamina=16; f.Tick(0.2f,7); Assert(!f.Audio.isPlaying,"breathing did not stop"); Near(f.Audio.volume,0,"audio reset"); Near(f.Noise.AmplitudeGain,1,"amplitude reset"); Near(f.Noise.FrequencyGain,1,"frequency reset"); Near(f.Chromatic.intensity.value,0.128f,"chromatic reset"); });
        Run("small recovery cannot clear latch",f=> { f.Exhaust(); f.Brain.CurrentStamina=7; f.Tick(); Assert(f.Brain.IsStaminaDepleted && f.Audio.isPlaying,"cleared before threshold"); });
        Run("re-exhaustion reverses partial fade without restarting audio",f=> { f.Exhaust(); f.Brain.CurrentStamina=16; f.Tick(0.4f); float falling=f.Audio.volume; f.Brain.CurrentStamina=0; f.Tick(0.1f); Assert(f.Audio.volume>falling && f.Audio.volume<0.7f && f.Audio.Plays==1,"fade reversal snapped/restarted"); });
        Run("same-frame exhaustion and healing do not play stale effects",f=> { f.Brain.CurrentStamina=0; f.Brain.CurrentStamina=100; f.Tick(); Assert(f.Events.Count==2 && f.Audio.Plays==0,"stale exhaustion"); });
        Run("pause freezes envelope and does not restart paused audio",f=> { f.Brain.CurrentStamina=0; f.Tick(); float volume=f.Audio.volume; int plays=f.Audio.Plays; PauseManager.IsPaused=true; f.Audio.isPlaying=false; f.Tick(1,5); Near(f.Audio.volume,volume,"pause advanced fade"); Assert(f.Audio.Plays==plays,"pause restarted audio"); f.Audio.isPlaying=true; PauseManager.IsPaused=false; f.Tick(); Assert(f.Audio.volume>volume,"resume did not advance"); });
        Run("death fades effects even with stamina still depleted",f=> { f.Exhaust(); Fixture.Set(f.Brain,"Condition",TargetCondition.Dead); f.Tick(0.2f,7); Assert(f.Brain.IsStaminaDepleted && !f.Audio.isPlaying,"death left breathing"); Near(f.Noise.AmplitudeGain,1,"death left noise"); });
        Run("disable immediately stops and restores effects",f=> { f.Exhaust(); Fixture.Call(f.Effects,"OnDisable"); Assert(!f.Audio.isPlaying,"disabled audio continued"); Near(f.Noise.AmplitudeGain,1,"disabled noise"); Near(f.Chromatic.intensity.value,0.128f,"disabled aberration"); });
        Run("enable synchronizes already-depleted stamina",f=> { Fixture.Call(f.Effects,"OnDisable"); f.Brain.CurrentStamina=0; Fixture.Call(f.Effects,"OnEnable"); f.Tick(); Assert(f.Audio.isPlaying,"enable missed current state"); });
        Run("runtime volume instance leaves shared profile unchanged",f=> { f.Exhaust(); Near(f.Volume.sharedProfile.Chromatic.intensity.value,0.128f,"shared profile modified"); Assert(!ReferenceEquals(f.Chromatic,f.Volume.sharedProfile.Chromatic),"profile not instanced"); });
        Run("disabled chromatic override is restored exactly",f=> { f.Chromatic.active=false; f.Chromatic.intensity.overrideState=false; f.Exhaust(); Assert(f.Chromatic.active && f.Chromatic.intensity.overrideState,"chromatic not enabled"); f.Brain.CurrentStamina=16; f.Tick(0.2f,7); Assert(!f.Chromatic.active && !f.Chromatic.intensity.overrideState,"override not restored"); });
        Run("camera's current baseline is used and restored",f=> { f.Noise.AmplitudeGain=3; f.Noise.FrequencyGain=0.65f; f.Exhaust(); Near(f.Noise.AmplitudeGain,6,"custom amplitude"); Near(f.Noise.FrequencyGain,0.8125f,"custom frequency"); f.Brain.CurrentStamina=16; f.Tick(0.2f,7); Near(f.Noise.AmplitudeGain,3,"custom amplitude reset"); Near(f.Noise.FrequencyGain,0.65f,"custom frequency reset"); });
        Run("inactive effects do not overwrite camera changes",f=> { f.Noise.AmplitudeGain=4; f.Tick(); Near(f.Noise.AmplitudeGain,4,"inactive effect overwrote camera"); });
        Run("configured durations control timing",f=> { Fixture.Set(f.Effects,"fadeInDuration",2f); Fixture.Set(f.Effects,"fadeOutDuration",4f); f.Brain.CurrentStamina=0; f.Tick(1); Near(f.Audio.volume,0.35f,"custom fade-in"); f.Tick(1); f.Brain.CurrentStamina=16; f.Tick(2); Near(f.Audio.volume,0.35f,"custom fade-out"); });
        using(var f=new Fixture(visuals:false)) { f.Exhaust(); Assert(f.Audio.isPlaying,"audio-only failed"); count++; Console.WriteLine("PASS audio without visual references"); }
        using(var f=new Fixture(audio:false)) { f.Exhaust(); Near(f.Noise.AmplitudeGain,2,"visual-only failed"); count++; Console.WriteLine("PASS visuals without audio reference"); }
        Console.WriteLine(count+" stamina/effect checks passed. Audio/rendering/camera are engine boundaries, not native Unity tests.");
        return 0;
    }
}
