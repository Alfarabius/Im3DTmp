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
    public PlayerDamageEffects Damage;
    public AudioSource Audio;
    public AudioClip BreathingClip,PainClip;
    public CinemachineBasicMultiChannelPerlin Noise;
    public Volume Volume;
    public ChromaticAberration Chromatic => Volume.profile.Chromatic;
    public Vignette Vignette => Volume.profile.Vignette;
    public float Redness => 1f-Vignette.color.value.g;
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
        Time.time=0;
        GameObject.SceneObjects.Clear();
        typeof(GameManager).GetProperty("Instance").SetValue(null,null);
        var player=new GameObject(); Brain=player.AddComponent<PlayerBrain>();
        Set(Brain,"Anim",player.AddComponent<Animator>());
        Audio=player.AddComponent<AudioSource>(); BreathingClip=new AudioClip(); PainClip=new AudioClip(); Audio.clip=BreathingClip;
        Noise=player.AddComponent<CinemachineBasicMultiChannelPerlin>();
        Volume=player.AddComponent<Volume>(); Volume.sharedProfile.Chromatic.intensity.value=0.128f; Volume.sharedProfile.Chromatic.intensity.overrideState=true;
        Volume.sharedProfile.Vignette.intensity.value=0.451f; Volume.sharedProfile.Vignette.color.value=Color.white;
        Effects=player.AddComponent<PlayerStaminaEffects>();
        player.AddComponent<CinemachineImpulseSource>();
        Damage=player.AddComponent<PlayerDamageEffects>();
        Set(Damage,"painClip",PainClip);
        Set(Damage,"postProcessVolume",Volume);
        if(audio) { Set(Effects,"breathingAudio",Audio); Set(Damage,"painAudio",Audio); }
        if(visuals) { Set(Effects,"cameraNoise",Noise); Set(Effects,"postProcessVolume",Volume); }
        Call(Brain,"Awake"); Brain.OnStaminaDepletedChanged+=Events.Add;
        Call(Effects,"Awake"); Call(Effects,"OnEnable");
        Call(Damage,"Awake"); Call(Damage,"OnEnable");
    }
    public void Tick(float seconds=0.1f,int count=1,bool stamina=true)
    {
        for(int i=0;i<count;i++)
        {
            Time.deltaTime=PauseManager.IsPaused ? 0 : seconds;
            Time.time+=Time.deltaTime;
            Call(Damage,"Update");
            if(stamina) Call(Effects,"LateUpdate");
            Call(Damage,"LateUpdate");
        }
    }
    public void Exhaust() { Brain.CurrentStamina=0; Tick(0.1f,8); }
    public void Dispose() { Call(Effects,"OnDisable"); Call(Damage,"OnDisable"); }
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
        Run("injury replaces exhaustion on the same audio source and clears fatigue visuals",f=>
        {
            f.Exhaust();
            f.Brain.TryApplyHit(1f); f.Tick(0.1f);
            Assert(f.Brain.Condition==TargetCondition.Wounded && f.Brain.IsStaminaDepleted,"injury did not deplete stamina");
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.isPlaying && f.Audio.Plays==2,"pain did not replace breathing");
            Near(f.Noise.AmplitudeGain,1,"injury left fatigue shake");
            Near(f.Noise.FrequencyGain,1,"injury left fatigue frequency");
            Near(f.Chromatic.intensity.value,0.128f,"injury left fatigue aberration");
            f.Tick(0.1f,20);
            Assert(f.Audio.Plays==2,"injury restarted voice");
            Near(f.Noise.AmplitudeGain,1,"fatigue returned while wounded");
            Near(f.Chromatic.intensity.value,0.128f,"aberration returned while wounded");
        });
        Run("a fresh injury starts only pain with a gradual onset",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.05f);
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.Plays==1,"fresh injury played breathing");
            Assert(f.Audio.volume>0 && f.Audio.volume<0.8f,"pain onset snapped");
            Near(f.Noise.AmplitudeGain,1,"fresh injury caused fatigue shake");
            f.Tick(0.05f,6); Near(f.Audio.volume,0.8f,"pain did not reach peak");
        });
        Run("heavy injury retains the pain clip without restarting it or enabling fatigue",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,4);
            int plays=f.Audio.Plays;
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,20);
            Assert(f.Brain.Condition==TargetCondition.HeavyWounded,"not heavy wounded");
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.Plays==plays,"heavy injury restarted/switched pain");
            Near(f.Audio.volume,0.8f,"heavy injury lost pain");
            Near(f.Noise.AmplitudeGain,1,"heavy injury enabled fatigue shake");
            Near(f.Chromatic.intensity.value,0.128f,"heavy injury enabled fatigue aberration");
        });
        Run("pain depends on injury even when stamina recovers",f=>
        {
            f.Brain.TryApplyHit(1f); f.Brain.CurrentStamina=100; f.Tick(0.1f,20);
            Assert(!f.Brain.IsStaminaDepleted && ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.isPlaying,"pain tied to stamina");
            Near(f.Audio.volume,0.8f,"pain faded while still wounded");
        });
        Run("healing gradually fades pain to silence and restores the breathing clip",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            f.Brain.Heal(); f.Tick(0.2f);
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.isPlaying,"healing abruptly switched/stopped pain");
            Assert(f.Audio.volume>0 && f.Audio.volume<0.8f,"healing did not fade pain");
            f.Tick(0.2f,6);
            Assert(!f.Audio.isPlaying && ReferenceEquals(f.Audio.clip,f.BreathingClip),"pain did not stop/restore clip");
            Near(f.Audio.volume,0,"healed voice not silent");
            f.Tick(1,3); Assert(f.Audio.Plays==1,"healing played exhaustion");
        });
        Run("repeat injury reverses pain fade without restarting the clip",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            f.Brain.Heal(); f.Tick(0.4f);
            float fading=f.Audio.volume; int plays=f.Audio.Plays;
            f.Brain.TryApplyHit(1f); f.Tick(0.05f);
            Assert(f.Audio.volume>fading && f.Audio.volume<0.8f && f.Audio.Plays==plays,"repeat injury snapped/restarted pain");
        });
        Run("damage manages pain and healing independently of disabled stamina effects",f=>
        {
            Fixture.Call(f.Effects,"OnDisable");
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5,stamina:false);
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.isPlaying,"damage did not start pain independently");
            Near(f.Audio.volume,0.8f,"independent pain peak");
            f.Brain.Heal(); f.Tick(0.2f,stamina:false);
            Assert(f.Audio.volume>0 && f.Audio.volume<0.8f,"independent healing did not fade pain");
            f.Tick(0.2f,6,stamina:false);
            Assert(!f.Audio.isPlaying && ReferenceEquals(f.Audio.clip,f.BreathingClip),"damage did not restore source independently");
        });
        Run("disabling stamina effects does not stop damage audio on the shared source",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            float volume=f.Audio.volume;
            Fixture.Call(f.Effects,"OnDisable");
            Assert(f.Audio.isPlaying && ReferenceEquals(f.Audio.clip,f.PainClip),"stamina disable interrupted pain");
            Near(f.Audio.volume,volume,"stamina disable changed pain volume");
            Fixture.Call(f.Damage,"OnDisable");
            Assert(!f.Audio.isPlaying && ReferenceEquals(f.Audio.clip,f.BreathingClip),"damage disable did not release source");
        });
        Run("exhaustion waits for pain to finish fading before reusing the source",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            f.Brain.Heal(); f.Brain.CurrentStamina=0; f.Tick(0.1f,11);
            Assert(ReferenceEquals(f.Audio.clip,f.PainClip) && f.Audio.isPlaying && f.Audio.volume<0.8f,"exhaustion interrupted pain fade");
            f.Tick(0.2f);
            Assert(ReferenceEquals(f.Audio.clip,f.BreathingClip) && !f.Audio.isPlaying,"damage did not release source after fade");
            f.Tick(0.1f);
            Assert(f.Audio.isPlaying && f.Audio.volume>0 && f.Audio.volume<0.7f,"breathing resumed abruptly");
        });
        Run("injury gently pulses only the red tint within a fixed vignette",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            float min=1,max=0;
            for(int i=0;i<120;i++)
            {
                f.Tick(0.02f); float red=f.Redness;
                Assert(red>=0.0999f && red<=0.3001f,"redness outside configured range");
                Near(f.Vignette.intensity.value,0.451f,"pulse changed vignette geometry");
                Near(f.Vignette.color.value.g,f.Vignette.color.value.b,"pulse changed tint hue");
                min=Math.Min(min,red); max=Math.Max(max,red);
            }
            Assert(max-min>0.19f,"redness did not pulse between its limits");
            Near(f.Vignette.color.value.r,1,"pulse darkened the red channel");
        });
        Run("wounded and heavy wounded use exactly the same tint pulse",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            float startTime=Time.time;
            var normal=new float[120];
            for(int i=0;i<normal.Length;i++) { f.Tick(0.02f); normal[i]=f.Redness; }
            Fixture.Set(f.Brain,"Condition",TargetCondition.HeavyWounded); Time.time=startTime;
            for(int i=0;i<normal.Length;i++)
            {
                f.Tick(0.02f);
                Near(f.Redness,normal[i],"wound severity changed the red tint");
                Near(f.Vignette.intensity.value,0.451f,"heavy wound changed vignette geometry");
            }
        });
        Run("hit flashes and healing preserve the volume's configured vignette shape",f=>
        {
            f.Vignette.intensity.value=0.37f;
            f.Brain.TryApplyHit(1f); f.Tick(0.05f);
            Near(f.Vignette.intensity.value,0.37f,"hit flash changed vignette geometry");
            f.Brain.TryApplyHit(1f); f.Tick(0.05f);
            Near(f.Vignette.intensity.value,0.37f,"second hit changed vignette geometry");
            f.Brain.Heal();
            for(int i=0;i<15;i++) { f.Tick(0.1f); Near(f.Vignette.intensity.value,0.37f,"healing resized vignette"); }
            Near(f.Redness,0,"healing left a tint");
        });
        Run("healthy player has a neutral tint with the configured vignette shape",f=>
        {
            f.Tick(); Near(f.Redness,0,"healthy player was tinted");
            Near(f.Vignette.color.value.r,1,"neutral tint altered red channel");
            Near(f.Vignette.color.value.b,1,"neutral tint altered blue channel");
            Near(f.Vignette.intensity.value,0.451f,"healthy update altered vignette geometry");
        });
        Run("healing fades redness and pain together with no upward pulse or abrupt stop",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            float start=f.Redness, previous=start, volume=f.Audio.volume;
            f.Brain.Heal();
            for(int i=0;i<11;i++)
            {
                f.Tick(0.1f); float red=f.Redness;
                Assert(red>0 && red<previous,"healing redness snapped or pulsed upwards");
                Assert(f.Audio.volume>0 && f.Audio.volume<volume && f.Audio.isPlaying,"healing audio snapped or rose");
                Near(red/start,f.Audio.volume/0.8f,"healing envelopes diverged");
                previous=red; volume=f.Audio.volume;
            }
            f.Tick(0.1f,2); Near(f.Redness,0,"healing left redness");
            Assert(!f.Audio.isPlaying,"healing left pain");
        });
        Run("pause freezes the healing audio and redness envelopes",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5); f.Brain.Heal(); f.Tick(0.2f);
            float red=f.Redness,volume=f.Audio.volume;
            PauseManager.IsPaused=true; f.Tick(1,5);
            Near(f.Redness,red,"pause advanced redness fade"); Near(f.Audio.volume,volume,"pause advanced pain fade");
            PauseManager.IsPaused=false; f.Tick(0.2f);
            Assert(f.Redness<red && f.Audio.volume<volume,"resume did not advance healing fade");
        });
        Run("injury effects only modify the runtime volume instance",f=>
        {
            f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            Assert(f.Redness>0,"no runtime wound effect");
            Near(f.Volume.sharedProfile.Vignette.intensity.value,0.451f,"injury modified shared vignette geometry");
            Near(f.Volume.sharedProfile.Vignette.color.value.g,1,"injury modified shared vignette tint");
            Near(f.Volume.sharedProfile.Color.saturation.value,0,"injury modified shared saturation");
        });
        Run("missing pain clip silences breathing without playing a null clip",f=>
        {
            f.Exhaust(); Fixture.Set(f.Damage,"painClip",null); f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            Assert(!f.Audio.isPlaying && f.Audio.clip==null && f.Audio.Plays==1,"missing pain played breathing/null clip");
            Near(f.Noise.AmplitudeGain,1,"missing pain left fatigue shake");
        });
        Run("death fades pain and disabling restores the original clip",f=>
        {
            f.Brain.TryApplyHit(1f); f.Brain.TryApplyHit(1f); f.Tick(0.1f,5);
            f.Brain.TryApplyHit(1f); f.Tick(0.1f);
            Assert(f.Brain.Condition==TargetCondition.Dead && f.Audio.volume>0 && f.Audio.volume<0.8f,"death did not fade pain");
            f.Tick(0.1f,12); Assert(!f.Audio.isPlaying,"death left pain");
            Fixture.Call(f.Effects,"OnDisable");
            Assert(ReferenceEquals(f.Audio.clip,f.BreathingClip),"disable left pain clip assigned");
        });
        Run("loot opening notifies effects once, heals and disables only the trigger",f=>
        {
            var box=new GameObject(); var solid=box.AddComponent<Collider>();
            var trigger=box.AddComponent<Collider>(); trigger.isTrigger=true;
            var loot=box.AddComponent<LootBox>(); Fixture.Set(loot,"_playerBrain",f.Brain);
            Fixture.Call(loot,"Awake");
            f.Brain.TryApplyHit(1f); int opened=0;
            loot.OnOpened+=()=>opened++;
            Fixture.Call(loot,"OpenBox"); Fixture.Call(loot,"OpenBox");
            Assert(opened==1 && !trigger.enabled && solid.enabled,"opening effects repeated or solid collider disabled");
            Assert(f.Brain.Condition==TargetCondition.Normal && f.Brain.CurrentStamina==100,"opening no longer heals");
        });
        Run("loot opening notifies effects before reporting victory",f=>
        {
            GameSessionController.Wins=0;
            var game=new GameObject().AddComponent<GameManager>(); Fixture.Set(game,"requiredLootBoxes",1); Fixture.Call(game,"Awake");
            var loot=new GameObject().AddComponent<LootBox>(); Fixture.Call(loot,"Awake");
            int winsDuringEffect=-1; loot.OnOpened+=()=>winsDuringEffect=GameSessionController.Wins;
            Fixture.Call(loot,"OpenBox");
            Assert(winsDuringEffect==0 && GameSessionController.Wins==1,"victory happened before opening effects");
        });
        using(var f=new Fixture(visuals:false)) { f.Exhaust(); Assert(f.Audio.isPlaying,"audio-only failed"); count++; Console.WriteLine("PASS audio without visual references"); }
        using(var f=new Fixture(audio:false)) { f.Exhaust(); Near(f.Noise.AmplitudeGain,2,"visual-only failed"); count++; Console.WriteLine("PASS visuals without audio reference"); }
        Console.WriteLine(count+" stamina/injury effect checks passed. Audio/rendering/camera are engine boundaries, not native Unity tests.");
        return 0;
    }
}
