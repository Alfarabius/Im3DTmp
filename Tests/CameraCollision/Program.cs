using System;
using UnityEngine;

class Program
{
    static int count;
    static void Assert(bool value,string message) { if(!value)throw new Exception(message); }
    static void Near(float value,float expected) => Assert(Math.Abs(value-expected)<0.00001f,value+" != "+expected);
    static void Run(string name,Action test) { test();count++;Console.WriteLine("PASS "+name); }
    static bool Push(Vector3 point,Vector3 half,float radius,out Vector3 result) =>
        CameraCollisionGeometry.PushSphereOutOfBox(point,half,radius,Vector3.forward,out result);

    static int Main()
    {
        Run("free camera stays in place",()=> { Assert(!Push(new Vector3(2,0,0),Vector3.one,0.2f,out var p),"free camera moved");Near(p.x,2); });
        Run("sphere overlapping a face is moved outside",()=> { Assert(Push(new Vector3(1.1f,0,0),Vector3.one,0.2f,out var p),"overlap missed");Near(p.x,1.2f); });
        Run("sphere inside thin body is moved toward the front",()=> { Push(Vector3.zero,new Vector3(1,1,0.1f),0.2f,out var p);Near(p.z,0.3f);Near(p.x,0); });
        Run("inside recovery chooses the nearest face",()=> { Push(new Vector3(0.95f,0,0),Vector3.one,0.2f,out var p);Near(p.x,1.2f);Near(p.z,0); });
        Run("negative side remains on the negative side",()=> { Push(new Vector3(-1.1f,0,0),Vector3.one,0.2f,out var p);Near(p.x,-1.2f); });
        Run("corner recovery respects spherical distance",()=>
        {
            Push(new Vector3(1.1f,1.1f,0),Vector3.one,0.2f,out var p);
            Near((p-new Vector3(1,1,0)).magnitude,0.2f);Near(p.x,p.y);
        });
        Run("scaled bone radius keeps a world sized clearance",()=>
        {
            float scale=40f,radius=0.15f;
            Push(Vector3.zero,new Vector3(0.0079f,0.009195f,0.00176655f),radius/scale,out var p);
            Near(p.z*scale,0.00176655f*scale+radius);
        });
        Run("segment catches tunnelling through the body",()=>
        {
            Assert(CameraCollisionGeometry.SegmentEntersBox(new Vector3(-2,0,0),new Vector3(2,0,0),Vector3.one,out float t),"body crossed");Near(t,0.25f);
        });
        Run("parallel clear segment is allowed",()=> { Assert(!CameraCollisionGeometry.SegmentEntersBox(new Vector3(-2,2,0),new Vector3(2,2,0),Vector3.one,out _),"clear segment blocked"); });
        Run("stationary clear camera is allowed",()=> { Assert(!CameraCollisionGeometry.SegmentEntersBox(new Vector3(2,0,0),new Vector3(2,0,0),Vector3.one,out _),"stationary point blocked"); });
        Run("a camera already inside may move out for recovery",()=> { Assert(!CameraCollisionGeometry.SegmentEntersBox(Vector3.zero,new Vector3(2,0,0),Vector3.one,out _),"inside recovery blocked"); });
        Run("contact does not allow the camera to enter the body next frame",()=>
        {
            Assert(CameraCollisionGeometry.SegmentEntersBox(new Vector3(1,0,0),new Vector3(-2,0,0),Vector3.one,out float t),"contact crossed body");Near(t,0);
        });
        Run("contact allows outward and tangential movement",()=>
        {
            Assert(!CameraCollisionGeometry.SegmentEntersBox(new Vector3(1,0,0),new Vector3(2,0,0),Vector3.one,out _),"outward movement blocked");
            Assert(!CameraCollisionGeometry.SegmentEntersBox(new Vector3(1,0,0),new Vector3(1,1,0),Vector3.one,out _),"tangential movement blocked");
        });
        Run("moving away from body is allowed",()=> { Assert(!CameraCollisionGeometry.SegmentEntersBox(new Vector3(2,0,0),new Vector3(3,0,0),Vector3.one,out _),"moving away blocked"); });
        Run("endpoint touching the body is detected",()=>
        {
            Assert(CameraCollisionGeometry.SegmentEntersBox(new Vector3(2,0,0),new Vector3(1,0,0),Vector3.one,out float t),"contact missed");Near(t,1);
        });
        Run("very thin body volumes still stop a sweep",()=>
        {
            Assert(CameraCollisionGeometry.SegmentEntersBox(new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(0.0001f,1,1),out float t),"thin volume missed");Near(t,0.49995f);
        });
        Run("random overlap recovery always separates sphere and body",()=>
        {
            var random=new System.Random(7);
            for(int i=0;i<10000;i++)
            {
                var half=new Vector3((float)random.NextDouble()+0.01f,(float)random.NextDouble()+0.01f,(float)random.NextDouble()+0.01f);
                var point=new Vector3((float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2);
                float radius=(float)random.NextDouble()*0.4f+0.01f;
                Push(point,half,radius,out var result);
                var closest=new Vector3(Mathf.Clamp(result.x,-half.x,half.x),Mathf.Clamp(result.y,-half.y,half.y),Mathf.Clamp(result.z,-half.z,half.z));
                Assert((result-closest).magnitude>=radius-0.00001f,"overlap remained after recovery");
            }
        });
        Console.WriteLine(count+" camera geometry checks passed. Native physics/rendering require the Unity Editor tests.");
        return 0;
    }
}
