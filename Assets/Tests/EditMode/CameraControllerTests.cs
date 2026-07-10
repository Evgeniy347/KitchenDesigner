using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CameraControllerTests
{
    private GameObject _cameraGo;
    private GameObject _floorGo;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _cameraGo.tag = "MainCamera";
        _cameraGo.AddComponent<Camera>();

        _floorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _floorGo.name = "TestFloor";
        _floorGo.tag = "Floor";
        _floorGo.transform.localScale = new Vector3(3, 0.018f, 3);
        _floorGo.transform.position = new Vector3(0, -0.009f, 0);

        var controllerGo = new GameObject("CameraController");
        _controller = controllerGo.AddComponent<CameraController>();
        _controller.AssignTestCamera(_cameraGo.GetComponent<Camera>());
        _controller.AssignTestFloor(_floorGo);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_controller.gameObject);
        Object.DestroyImmediate(_floorGo);
        Object.DestroyImmediate(_cameraGo);
    }

    [Test]
    public void UpdateCameraPosition_PositionsCameraBehindTarget()
    {
        _controller.SetState(new CameraState
        {
            valid = true,
            targetX = 2f, targetY = 1f, targetZ = 3f,
            angleX = 0f, angleY = 0f, distance = 5f
        });
        _controller.UpdateCameraPosition();

        Assert.AreEqual(new Vector3(2f, 1f, -2f), _cameraGo.transform.position,
            "camera should be 5 units behind target along -Z");

        Vector3 expectedForward = (new Vector3(2f, 1f, 3f) - _cameraGo.transform.position).normalized;
        Assert.AreEqual(expectedForward, _cameraGo.transform.forward.normalized,
            "camera should look at target");
    }

    [Test]
    public void UpdateCameraPosition_OrbitsAroundTarget()
    {
        _controller.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = -90f, distance = 5f
        });
        _controller.UpdateCameraPosition();

        Assert.AreEqual(5f, _cameraGo.transform.position.x, 0.001f, "camera X should be 5m after 90 deg right orbit");
        Assert.AreEqual(0f, _cameraGo.transform.position.y, 0.001f, "camera Y should be 0m");
        Assert.AreEqual(0f, _cameraGo.transform.position.z, 0.001f, "camera Z should be 0m");
    }

    [Test]
    public void UpdateFloorVisibility_DisablesRenderer_WhenCameraBelowFloorTop()
    {
        _cameraGo.transform.position = new Vector3(0, -1f, 0);
        _controller.UpdateFloorVisibility();

        var renderer = _floorGo.GetComponent<MeshRenderer>();
        Assert.IsFalse(renderer.enabled, "floor should be invisible when camera is below it");
    }

    [Test]
    public void UpdateFloorVisibility_EnablesRenderer_WhenCameraAboveFloorTop()
    {
        _cameraGo.transform.position = new Vector3(0, 10f, 0);
        _controller.UpdateFloorVisibility();

        var renderer = _floorGo.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer.enabled, "floor should be visible when camera is above it");
    }

    [Test]
    public void UpdateFloorVisibility_DisablesCollider_WhenCameraBelowFloorTop()
    {
        _cameraGo.transform.position = new Vector3(0, -1f, 0);
        _controller.UpdateFloorVisibility();

        var collider = _floorGo.GetComponent<Collider>();
        Assert.IsFalse(collider.enabled, "floor collider should be disabled when camera is below it");
    }

    [Test]
    public void UpdateFloorVisibility_EnablesCollider_WhenCameraAboveFloorTop()
    {
        _cameraGo.transform.position = new Vector3(0, 10f, 0);
        _controller.UpdateFloorVisibility();

        var collider = _floorGo.GetComponent<Collider>();
        Assert.IsTrue(collider.enabled, "floor collider should be enabled when camera is above it");
    }

    [Test]
    public void UpdateFloorVisibility_DoesNotThrow_WhenFloorIsNull()
    {
        Object.DestroyImmediate(_floorGo);
        _floorGo = null;

        _cameraGo.transform.position = new Vector3(0, 10f, 0);
        _controller.UpdateFloorVisibility();
    }

    [Test]
    public void UpdateFloorVisibility_DoesNotThrow_WhenCameraIsNull()
    {
        Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        _controller.UpdateFloorVisibility();
    }

    [Test]
    public void UpdateCameraPosition_DoesNotThrow_WhenCameraIsNull()
    {
        Object.DestroyImmediate(_cameraGo);
        _cameraGo = null;

        _controller.UpdateCameraPosition();
    }
}
