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
            angleX = 0f, angleY = 90f, distance = 5f
        });

        Assert.AreEqual(new Vector3(5f, 0f, 0f), _cameraGo.transform.position,
            "camera should orbit 90 degrees to the right");
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
