// Geometry checks only; Unity physics and rendering are covered by Editor tests.
using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public float this[int i]
        {
            get => i==0 ? x : i==1 ? y : z;
            set { if(i==0)x=value;else if(i==1)y=value;else z=value; }
        }
        public static Vector3 zero => new Vector3();
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 forward => new Vector3(0,0,1);
        public float magnitude => (float)Math.Sqrt(x*x+y*y+z*z);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public static class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static float Sign(float value) => value>=0 ? 1 : -1;
        public static float Min(float a,float b) => Math.Min(a,b);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Clamp(float value,float min,float max) => Math.Max(min,Math.Min(value,max));
    }
}
