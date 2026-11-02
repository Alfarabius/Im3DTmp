using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public class FakeTarget : MonoBehaviour, ITargetable
{
    public bool Player;
    public float TargetWeight => 1;
    public Transform TargetTransform => transform;
    public TargetCondition Condition { get; set; }
    public void TryApplyHit(float chance) {}
    public bool IsPlayer() => Player;
}
class Fixture : IDisposable
{
    public EnemyBrain Brain;
    public PatrolState Patrol;
    public HuntState Hunt;
    public ChaseState Chase;
    public AttackState Attack;
    public NavMeshAgent Agent;
    public GameManager Game;
    public Transform Player, A, B;
    public static void Set(object target,string name,object value)
    {
        var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(field!=null) field.SetValue(target,value);
        else target.GetType().GetProperty(name).SetValue(target,value);
    }
    public static object Call(object target,string method) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    public static object Read(object target,string field) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    public ICharacterState State => (ICharacterState)Read(Brain,"_currentState");
    public Transform Point => (Transform)Read(Hunt,"_currentPoint");
    public Fixture(bool start=true)
    {
        Physics.Targets.Clear(); Physics.Blocked=false; Time.deltaTime=1;
        GameSessionController.Wins=0; GameSessionController.Restarts=0;
        typeof(GameManager).GetProperty("Instance").SetValue(null,null);
        Game=new GameObject().AddComponent<GameManager>(); Set(Game,"requiredLootBoxes",100); Call(Game,"Awake");
        var enemy=new GameObject(); Agent=enemy.AddComponent<NavMeshAgent>(); enemy.AddComponent<Animator>();
        Brain=enemy.AddComponent<EnemyBrain>(); Patrol=enemy.AddComponent<PatrolState>(); Hunt=enemy.AddComponent<HuntState>(); Chase=enemy.AddComponent<ChaseState>(); Attack=enemy.AddComponent<AttackState>();
        Set(Brain,"targetLayers",(LayerMask)(-1)); Set(Brain,"visionRadius",35f); Set(Brain,"visionAngle",60f); Set(Brain,"AttackDistance",20f); Set(Brain,"MaxStamina",150f);
        var route=new GameObject().transform; A=new GameObject().transform; B=new GameObject().transform;
        A.position=new Vector3(50,0,0); B.position=new Vector3(80,0,0); route.AddChild(A); route.AddChild(B);
        Player=new GameObject().transform; Player.position=new Vector3(100,0,0);
        Set(Patrol,"routeRoot",route); Set(Hunt,"playerTransform",Player);
        Call(Brain,"Awake"); Call(Patrol,"Awake"); Call(Hunt,"Awake"); Call(Chase,"Awake"); Call(Attack,"Awake");
        Call(Brain,"OnEnable"); Call(Patrol,"Start");
        if(start) Start();
    }
    public void Start() { var start=(IEnumerator)Call(Brain,"Start"); start.MoveNext(); start.MoveNext(); }
    public void Tick(int count=1) { for(int i=0;i<count;i++) Call(Brain,"Update"); }
    public FakeTarget Target(bool player,Vector3 position)
    {
        GameObject go=player ? Player.gameObject : new GameObject(); go.transform.position=position;
        var target=go.AddComponent<FakeTarget>(); target.Player=player; Physics.Targets.Add(go.AddComponent<Collider>()); return target;
    }
    public void Dispose() { Call(Brain,"OnDisable"); }
}
class Program
{
    static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    static string SelectLootBoxes(int total,int seed=1,int? limit=null)
    {
        GameObject.SceneObjects.Clear();
        typeof(GameManager).GetProperty("Instance").SetValue(null,null);
        UnityEngine.Random.InitState(seed);
        var boxes=new LootBox[total];
        for(int i=0;i<total;i++) boxes[i]=new GameObject().AddComponent<LootBox>();
        var inactive=new GameObject(); inactive.AddComponent<LootBox>(); inactive.SetActive(false);
        var unrelated=new GameObject();
        var game=new GameObject().AddComponent<GameManager>();
        if(limit.HasValue) Fixture.Set(game,"activeLootBoxesCount",limit.Value);
        Fixture.Call(game,"Awake");
        int expected=Math.Min(total,Math.Max(0,limit ?? 10));
        Assert(boxes.Count(box=>box.gameObject.activeSelf)==expected,"wrong active loot box count for "+total);
        Assert(!inactive.activeSelf && unrelated.activeSelf,"changed an inactive box or unrelated object");
        Assert(GameSessionController.Wins==0,"selection counted as opening loot boxes");
        return string.Join(",",boxes.Select((box,index)=>box.gameObject.activeSelf ? index.ToString() : ""));
    }
    static void CheckLootBoxSelection()
    {
        GameSessionController.Wins=0;
        foreach(int total in new[] {0,6,10,24})
        {
            SelectLootBoxes(total);
            Console.WriteLine("PASS startup loot selection with "+total+" boxes");
        }
        Assert(SelectLootBoxes(24,seed:1)!=SelectLootBoxes(24,seed:2),"selection always kept the same boxes");
        Console.WriteLine("PASS different random seeds select different loot boxes");
        SelectLootBoxes(24,limit:3); SelectLootBoxes(24,limit:0);
        Console.WriteLine("PASS inspector loot limit controls active box count");
        GameObject.SceneObjects.Clear();
    }
    static void Run(string name,Action<Fixture> test)
    {
        using(var f=new Fixture()) test(f);
        Console.WriteLine("PASS "+name);
    }
    static int Main()
    {
        CheckLootBoxSelection();
        Run("10 seconds in patrol enter Hunt", f=> { f.Tick(9); Assert(f.State==f.Patrol,"early hunt"); f.Tick(); Assert(f.State==f.Hunt,"timer did not enter Hunt"); });
        Run("look-around time counts toward patrol timeout", f=> { Fixture.Set(f.Patrol,"rotationSpeed",1f); f.Tick(10); Assert(f.State==f.Hunt,"look-around blocked timer"); });
        Run("patrol re-entry resets timeout", f=> { f.Tick(9); f.Brain.ChangeState(f.Chase); f.Brain.ChangeState(f.Patrol); f.Tick(9); Assert(f.State==f.Patrol,"timer not reset"); f.Tick(); Assert(f.State==f.Hunt,"reset timer did not expire"); });
        Run("box 1 does not hunt, box 2 does", f=> { f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Patrol,"odd count triggered"); f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Hunt,"even count did not trigger"); });
        Run("each second box retriggers Hunt, odd counts do not", f=> { f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.Point==f.B,"initial point"); f.Player.position=f.A.position; f.Game.RegisterLootBoxOpened(); Assert(f.Point==f.B,"odd count retargeted"); f.Game.RegisterLootBoxOpened(); Assert(f.Point==f.A,"fourth box did not retrigger"); });
        Run("loot trigger works with loot victory disabled", f=> { Fixture.Set(f.Game,"enableLootBoxWin",false); f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Hunt,"win toggle suppressed hunt"); });
        Run("winning box ends game instead of triggering Hunt", f=> { Fixture.Set(f.Game,"requiredLootBoxes",2); f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(GameSessionController.Wins==1 && f.State==f.Patrol,"hunt ran after win"); f.Game.RegisterLootBoxOpened(); Assert(GameSessionController.Wins==1,"ended game counted more boxes"); });
        using(var f=new Fixture(false)) { f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.State==null,"started before readiness"); f.Start(); Assert(f.State==f.Hunt,"startup lost queued hunt"); Console.WriteLine("PASS queued hunt before startup"); }
        Run("disabled brain unsubscribes from loot events", f=> { Fixture.Call(f.Brain,"OnDisable"); f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Patrol,"disabled brain reacted"); });
        Run("Hunt interrupts attack and clears NPC target", f=> { f.Target(false,new Vector3(0,0,2)); f.Brain.ScanForTargets(); f.Brain.ChangeState(f.Attack); f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Hunt && f.Brain.CurrentTarget==null,"NPC target retained"); Assert(!f.Brain.Anim.Bools["IsAiming"],"aiming retained"); });
        Run("Hunt interrupts chase and resets omni radius", f=> { f.Brain.ChangeState(f.Chase); f.Game.RegisterLootBoxOpened(); f.Game.RegisterLootBoxOpened(); Assert(f.State==f.Hunt && f.Brain.CurrentOmniRadius==f.Brain.BaseOmniRadius,"chase state leaked"); });
        Run("NPC is ignored during Hunt", f=> { f.Target(false,new Vector3(0,0,2)); f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Hunt && f.Brain.CurrentTarget==null,"NPC registered"); });
        Run("visible close player enters Attack despite closer NPC", f=> { f.Target(false,new Vector3(0,0,2)); var player=f.Target(true,new Vector3(0,0,10)); f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Attack && f.Brain.CurrentTarget==player,"wrong close target/state"); });
        Run("visible distant player enters Chase despite closer NPC", f=> { f.Target(false,new Vector3(0,0,2)); var player=f.Target(true,new Vector3(0,0,30)); f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Chase && f.Brain.CurrentTarget==player,"wrong distant target/state"); });
        Run("occluded player does not interrupt Hunt", f=> { f.Target(true,new Vector3(0,0,10)); Physics.Blocked=true; f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Hunt,"saw through obstacle"); });
        Run("dead player does not interrupt Hunt", f=> { var p=f.Target(true,new Vector3(0,0,10)); p.Condition=TargetCondition.Dead; f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Hunt,"registered dead player"); });
        Run("attack distance boundary enters Attack", f=> { f.Target(true,new Vector3(0,0,20)); f.Brain.BeginHunt(); f.Tick(); Assert(f.State==f.Attack,"boundary went to Chase"); });
        Run("default mode keeps initial point while player moves", f=> { f.Brain.BeginHunt(); Assert(f.Point==f.B,"wrong nearest point"); f.Player.position=f.A.position; f.Tick(); Assert(f.Point==f.B,"fixed point changed"); });
        Run("checkbox enables point updates", f=> { Fixture.Set(f.Hunt,"updatePatrolPointWhileRunning",true); f.Brain.BeginHunt(); f.Player.position=f.A.position; f.Tick(); Assert(f.Point==f.A,"moving mode did not update"); });
        Run("turning checkbox off freezes current point", f=> { Fixture.Set(f.Hunt,"updatePatrolPointWhileRunning",true); f.Brain.BeginHunt(); f.Player.position=f.A.position; f.Tick(); Fixture.Set(f.Hunt,"updatePatrolPointWhileRunning",false); f.Player.position=new Vector3(100,0,0); f.Tick(); Assert(f.Point==f.A,"unchecked mode retargeted"); });
        Run("unchanged nearest point does not rebuild path", f=> { Fixture.Set(f.Hunt,"updatePatrolPointWhileRunning",true); f.Brain.BeginHunt(); int requests=f.Agent.DestinationRequests; f.Tick(3); Assert(f.Agent.DestinationRequests==requests,"redundant path requests"); });
        Run("zero stamina still runs and remains zero", f=> { Fixture.Set(f.Brain,"CurrentStamina",0f); f.Brain.BeginHunt(); f.Tick(20); Assert(f.State==f.Hunt && f.Agent.speed==5f && f.Brain.CurrentStamina==0f,"exhausted hunt changed speed/stamina"); });
        Run("nonzero stamina is neither drained nor regenerated", f=> { Fixture.Set(f.Brain,"CurrentStamina",45f); f.Brain.BeginHunt(); f.Tick(20); Assert(f.State==f.Hunt && f.Brain.CurrentStamina==45f,"stamina changed"); });
        Run("arrival returns to Patrol and resets timer", f=> { f.Brain.BeginHunt(); f.Agent.Warp(f.Agent.destination); f.Tick(); Assert(f.State==f.Patrol && (float)Fixture.Read(f.Patrol,"_timeInState")==0,"arrival did not return"); });
        Run("player detection takes priority over arrival", f=> { f.Brain.BeginHunt(); f.Agent.Warp(f.Agent.destination); f.Target(true,f.Agent.transform.position+new Vector3(0,0,10)); f.Tick(); Assert(f.State==f.Attack,"arrival hid visible player"); });
        Run("missing route returns to Patrol", f=> { Fixture.Set(f.Patrol,"routeRoot",null); f.Brain.BeginHunt(); Assert(f.State==f.Patrol,"missing route stuck in Hunt"); });
        Run("partial path returns to Patrol", f=> { f.Brain.BeginHunt(); f.Agent.pathStatus=NavMeshPathStatus.PathPartial; f.Tick(); Assert(f.State==f.Patrol,"partial path stuck in Hunt"); });
        Console.WriteLine("33 behavior checks passed. Native Unity behavior is not covered by these engine stubs.");
        return 0;
    }
}
