using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraCollisionTests
{
    private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);
    private GameObject _player;
    private Camera _camera;
    private Component _protection;
    private GameObject _wall;
    private GameObject _secondWall;

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        _player = new GameObject("CameraCollisionTestPlayer");
        _player.transform.position = Origin;
        Type brainType = Type.GetType("PlayerBrain, Assembly-CSharp", true);
        Component brain = _player.AddComponent(brainType);
        var controller = _player.GetComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.3f;
        controller.center = Vector3.up;
        brainType.GetProperty("Controller").SetValue(brain, controller);

        var cameraObject = new GameObject("CameraCollisionTestCamera");
        cameraObject.transform.SetParent(_player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1f, 2f);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.nearClipPlane = 0.02f;
        _camera.fieldOfView = 60f;
        _camera.aspect = 16f / 9f;
        _protection = cameraObject.AddComponent(Type.GetType("PlayerCameraCollision, Assembly-CSharp", true));
        Set(_protection, "player", brain);
        Set(_protection, "bodyRoot", _player.transform);
        Set(_protection, "environmentMask", (LayerMask)(1 << 0));
        var probeField = _protection.GetType().GetField("_probe", BindingFlags.Instance | BindingFlags.NonPublic);
        if (probeField.GetValue(_protection) == null) Call(_protection, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        if (_protection != null)
        {
            Call(_protection, "OnDisable");
            var probe = (Collider)_protection.GetType().GetField("_probe", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_protection);
            if (probe != null) UnityEngine.Object.DestroyImmediate(probe.gameObject);
        }
        if (_wall != null) UnityEngine.Object.DestroyImmediate(_wall);
        if (_secondWall != null) UnityEngine.Object.DestroyImmediate(_secondWall);
        if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
    }

    private static GameObject Wall(Vector3 position, Vector3 size)
    {
        var wall = new GameObject("CameraCollisionTestWall");
        wall.transform.position = Origin + position;
        wall.AddComponent<BoxCollider>().size = size;
        return wall;
    }

    private void Begin() => Call(_protection, "BeginCameraRendering", new ScriptableRenderContext(), _camera);
    private void End() => Call(_protection, "EndCameraRendering", new ScriptableRenderContext(), _camera);

    [Test]
    public void ThinWallStopsCameraAndRestoringPosePreventsFeedback()
    {
        _wall = Wall(new Vector3(0f, 1f, 1f), new Vector3(10f, 10f, 0.02f));
        Vector3 intended = _camera.transform.position;
        for (int i = 0; i < 5; i++)
        {
            Begin();
            Assert.That(_camera.transform.position.z - Origin.z, Is.LessThan(0.95f));
            End();
            Assert.That(Vector3.Distance(_camera.transform.position, intended), Is.LessThan(0.001f));
        }
    }

    [Test]
    public void TwoWallsConstrainCameraInACorner()
    {
        _wall = Wall(new Vector3(0f, 1f, 1f), new Vector3(10f, 10f, 0.02f));
        _secondWall = Wall(new Vector3(1f, 1f, 0f), new Vector3(0.02f, 10f, 10f));
        _camera.transform.position = Origin + new Vector3(2f, 1f, 2f);
        Begin();
        Vector3 position = _camera.transform.position - Origin;
        Assert.That(position.x, Is.LessThan(0.95f));
        Assert.That(position.z, Is.LessThan(0.95f));
    }

    [Test]
    public void OverlapRecoveryUsesRealPhysicsWithADisabledProbe()
    {
        _wall = Wall(new Vector3(0f, 1f, 1f), new Vector3(1f, 1f, 0.1f));
        Physics.SyncTransforms();
        object[] args = { Origin + new Vector3(0f, 1f, 1f), 0.05f };
        Assert.That(Call(_protection, "PushOutOfEnvironment", args), Is.True);
        Vector3 corrected = (Vector3)args[0];
        Assert.That(Mathf.Abs(corrected.z - Origin.z - 1f), Is.GreaterThan(0.099f));
    }

    [Test]
    public void TriggerAndPlayersControllerDoNotBlockCamera()
    {
        _wall = Wall(new Vector3(0f, 1f, 1f), new Vector3(10f, 10f, 0.02f));
        _wall.GetComponent<BoxCollider>().isTrigger = true;
        Vector3 intended = _camera.transform.position;
        Begin();
        Assert.That(Vector3.Distance(_camera.transform.position, intended), Is.LessThan(0.001f));
    }

    [Test]
    public void BodyProtectionFollowsBoneTranslationAndRotation()
    {
        var bone = new GameObject("head");
        bone.transform.SetParent(_player.transform, false);
        Type volumeType = _protection.GetType().GetNestedType("BodyVolume", BindingFlags.NonPublic);
        object volume = Activator.CreateInstance(volumeType, true);
        volumeType.GetField("boneName").SetValue(volume, "head");
        volumeType.GetField("size").SetValue(volume, new Vector3(0.3f, 0.4f, 0.1f));
        Array volumes = Array.CreateInstance(volumeType, 1);
        volumes.SetValue(volume, 0);
        Set(_protection, "bodyVolumes", volumes);
        Call(_protection, "CacheBones");
        for (int i = 0; i < 3; i++)
        {
            bone.transform.localPosition = new Vector3(i * 0.2f, 1f, 0.4f);
            bone.transform.localRotation = Quaternion.Euler(0f, i * 35f, 0f);
            _camera.transform.position = bone.transform.position;
            Begin();
            Vector3 local = bone.transform.InverseTransformPoint(_camera.transform.position);
            Assert.That(Mathf.Abs(local.x) > 0.19f || Mathf.Abs(local.y) > 0.24f || Mathf.Abs(local.z) > 0.09f, Is.True);
            End();
        }
    }

    [Test]
    public void CollisionRadiusEnclosesNearClipPlaneCorners()
    {
        _camera.nearClipPlane = 0.1f;
        _camera.fieldOfView = 100f;
        var corners = new Vector3[4];
        _camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), 0.1f, Camera.MonoOrStereoscopicEye.Mono, corners);
        float radius = (float)Call(_protection, "GetCameraRadius");
        foreach (Vector3 corner in corners) Assert.That(radius, Is.GreaterThan(corner.magnitude));
    }

    [Test]
    public void BodyRecoveryCannotPushTheCameraThroughAThinWall()
    {
        _wall = Wall(new Vector3(0f, 1f, 0.48f), new Vector3(10f, 10f, 0.02f));
        var bone = new GameObject("head");
        bone.transform.SetParent(_player.transform, false);
        bone.transform.localPosition = new Vector3(0f, 1f, 0.4f);
        Type volumeType = _protection.GetType().GetNestedType("BodyVolume", BindingFlags.NonPublic);
        object volume = Activator.CreateInstance(volumeType, true);
        volumeType.GetField("boneName").SetValue(volume, "head");
        volumeType.GetField("size").SetValue(volume, new Vector3(0.3f, 0.4f, 0.1f));
        Array volumes = Array.CreateInstance(volumeType, 1);
        volumes.SetValue(volume, 0);
        Set(_protection, "bodyVolumes", volumes);
        Call(_protection, "CacheBones");
        _camera.transform.position = bone.transform.position;
        Begin();
        Assert.That(_camera.transform.position.z - Origin.z, Is.LessThan(0.44f));
        Assert.That(Vector3.Distance(_camera.transform.position, bone.transform.position), Is.GreaterThan(0.14f));
    }
}
