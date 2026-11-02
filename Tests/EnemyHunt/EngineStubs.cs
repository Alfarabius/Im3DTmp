// Only the engine boundary is simulated. The tests compile and execute the real game scripts.
// This does not simulate Unity's native NavMesh, physics, rendering or animation.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public enum FindObjectsSortMode { None, InstanceID }
    public class Object
    {
        public static void Destroy(Object value, float delay=0) {}
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T:class =>
            GameObject.SceneObjects.Where(go=>go.activeSelf).Select(go=>go.GetComponent<T>()).Where(component=>component!=null).ToArray();
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component {}
    public class GameObject : Object
    {
        public static readonly List<GameObject> SceneObjects = new List<GameObject>();
        readonly List<Component> components = new List<Component>();
        public Transform transform;
        public int layer;
        public bool activeSelf=true;
        public GameObject(string name="") { transform = AddComponent<Transform>(); SceneObjects.Add(this); }
        public void SetActive(bool value) { activeSelf=value; }
        public T AddComponent<T>() where T : Component, new() { var value = new T { gameObject = this }; components.Add(value); return value; }
        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault();
    }
    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 forward = Vector3.forward;
        readonly List<Transform> children = new List<Transform>();
        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public void AddChild(Transform child) => children.Add(child);
        public Quaternion rotation { get => new Quaternion(forward); set => forward = value.direction; }
        public void Rotate(float x, float y, float z) => forward = Quaternion.Euler(x, y, z) * forward;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 forward => new Vector3(0,0,1);
        public float sqrMagnitude => x*x+y*y+z*z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this / magnitude : zero;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x/b,a.y/b,a.z/b);
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static float Angle(Vector3 a, Vector3 b)
        {
            float norm=a.magnitude*b.magnitude;
            return norm==0 ? 0 : (float)(Math.Acos(Math.Max(-1,Math.Min(1,(a.x*b.x+a.y*b.y+a.z*b.z)/norm)))*180/Math.PI);
        }
    }
    public struct Quaternion
    {
        public Vector3 direction;
        public Quaternion(Vector3 value) { direction=value; }
        public static Quaternion LookRotation(Vector3 direction) => new Quaternion(direction.normalized);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => b;
        public static Quaternion Euler(float x, float y, float z) => new Quaternion(new Vector3((float)Math.Sin(y*Math.PI/180),0,(float)Math.Cos(y*Math.PI/180)));
        public static Vector3 operator *(Quaternion q, Vector3 v) => new Vector3(q.direction.z*v.x+q.direction.x*v.z,v.y,-q.direction.x*v.x+q.direction.z*v.z);
    }
    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) => m.value;
        public static implicit operator LayerMask(int value) => new LayerMask { value=value };
    }
    public static class Time { public static float deltaTime = 1,time; }
    public static class Mathf
    {
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Clamp(float v,float a,float b) => Math.Max(a,Math.Min(v,b));
        public static int Clamp(int v,int a,int b) => Math.Max(a,Math.Min(v,b));
        public static float Clamp01(float value) => Clamp(value,0,1);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
        public static float MoveTowards(float current,float target,float delta) => Math.Abs(target-current)<=delta ? target : current+Math.Sign(target-current)*delta;
        public static float SmoothStep(float a,float b,float t) { t=Clamp01(t); return a+(b-a)*t*t*(3-2*t); }
        public static float Sin(float value) => (float)Math.Sin(value);
    }
    public static class Random
    {
        public static float value=0.5f;
        static System.Random generator=new System.Random(1);
        public static void InitState(int seed) { generator=new System.Random(seed); }
        public static int Range(int min,int max) => generator.Next(min,max);
        public static float Range(float min,float max) => min;
    }
    public class Animator : Component
    {
        public readonly Dictionary<string,bool> Bools = new Dictionary<string,bool>();
        public void SetBool(string key,bool value) => Bools[key]=value;
        public void SetFloat(string key,float value) {}
        public void SetTrigger(string key) {}
    }
    public class AudioClip : Object { public float length=4; }
    public enum AudioRolloffMode { Logarithmic, Linear }
    public class AudioSource : Component
    {
        public AudioClip clip;
        public float volume=1,spatialBlend=1;
        public bool loop,playOnAwake=true,isPlaying;
        public AudioRolloffMode rolloffMode;
        public float minDistance,maxDistance;
        public object outputAudioMixerGroup;
        public int Plays,Stops;
        public void PlayOneShot(AudioClip clip) {}
        public void Play() { Plays++; isPlaying=true; }
        public void Stop() { Stops++; isPlaying=false; }
    }
    public class Collider : Component {}
    public struct RaycastHit { public Collider collider; }
    public static class Physics
    {
        public static readonly List<Collider> Targets = new List<Collider>();
        public static bool Blocked;
        public static Collider[] OverlapSphere(Vector3 point,float radius,LayerMask mask) => Targets.Where(c => (mask.value & (1<<c.gameObject.layer)) != 0 && Vector3.Distance(point,c.transform.position)<=radius).ToArray();
        public static bool Raycast(Vector3 origin,Vector3 direction,float distance,LayerMask mask) => Blocked;
        public static bool Raycast(Vector3 origin,Vector3 direction,out RaycastHit hit,float distance,LayerMask mask) { hit=new RaycastHit(); return false; }
    }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]
    public class RequireComponent : Attribute { public RequireComponent(Type a) {} public RequireComponent(Type a,Type b) {} }
    public class SerializeField : Attribute {}
    public class Header : Attribute { public Header(string text) {} }
    public class Tooltip : Attribute { public Tooltip(string text) {} }
    public class Range : Attribute { public Range(float min,float max) {} }
    public class Min : Attribute { public Min(float min) {} }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) {} }
}
namespace UnityEngine.AI
{
    using UnityEngine;
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public struct NavMeshHit { public Vector3 position; }
    public static class NavMesh
    {
        public const int AllAreas=-1;
        public static bool SamplePosition(Vector3 value,out NavMeshHit hit,float distance,int areas) { hit=new NavMeshHit { position=value }; return true; }
    }
    public class NavMeshAgent : Component
    {
        public float speed,stoppingDistance;
        public Vector3 velocity;
        public Vector3 destination;
        public bool isStopped,pathPending;
        public bool isActiveAndEnabled=true,isOnNavMesh=true;
        public NavMeshPathStatus pathStatus;
        public float remainingDistance => Vector3.Distance(transform.position,destination);
        public int DestinationRequests;
        public bool SetDestination(Vector3 value) { DestinationRequests++; destination=value; return true; }
        public void ResetPath() { destination=transform.position; pathStatus=NavMeshPathStatus.PathComplete; }
        public bool Warp(Vector3 value) { transform.position=value; return true; }
    }
}
// Other gameplay boundaries; TargetCondition has the same values as NPCBrain.cs.
public enum TargetCondition { Normal, Wounded, HeavyWounded, Dead }
public class LootBox : UnityEngine.MonoBehaviour {}
public static class GameSessionController
{
    public static int Wins, Restarts;
    public static void CompleteGame() { Wins++; }
    public static void RestartGame() { Restarts++; }
}
