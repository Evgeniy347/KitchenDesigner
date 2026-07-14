using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;

public class CameraControllerTests
{
    private GameObject? _cameraGo;
    private GameObject? _floorGo;
    private CameraController? _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _cameraGo!.tag = "MainCamera";
        _cameraGo!.AddComponent<Camera>();

        _floorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _floorGo!.name = "TestFloor";
        _floorGo!.tag = "Floor";
        _floorGo!.transform.localScale = new Vector3(3, 0.018f, 3);
        _floorGo!.transform.position = new Vector3(0, -0.009f, 0);

        var controllerGo = new GameObject("CameraController");
        _controller = controllerGo.AddComponent<CameraController>();
        _controller!.AssignTestCamera(_cameraGo!.GetComponent<Camera>());
        _controller!.AssignTestFloor(_floorGo);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_controller!.gameObject);
        Object.DestroyImmediate(_floorGo!);
        Object.DestroyImmediate(_cameraGo!);
    }

    [Test]
    public void UpdateCameraPosition_PositionsCameraBehindTarget()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 2f, targetY = 1f, targetZ = 3f,
            angleX = 0f, angleY = 0f, distance = 5f
        });
        _controller!.UpdateCameraPosition();

        Assert.AreEqual(new Vector3(2f, 1f, -2f), _cameraGo!.transform.position,
            "camera should be 5 units behind target along -Z");

        Vector3 expectedForward = (new Vector3(2f, 1f, 3f) - _cameraGo!.transform.position).normalized;
        Assert.AreEqual(expectedForward, _cameraGo!.transform.forward.normalized,
            "camera should look at target");
    }

    [Test]
    public void UpdateCameraPosition_OrbitsAroundTarget()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = -90f, distance = 5f
        });
        _controller!.UpdateCameraPosition();

        Assert.AreEqual(5f, _cameraGo!.transform.position.x, 0.001f, "camera X should be 5m after 90 deg right orbit");
        Assert.AreEqual(0f, _cameraGo!.transform.position.y, 0.001f, "camera Y should be 0m");
        Assert.AreEqual(0f, _cameraGo!.transform.position.z, 0.001f, "camera Z should be 0m");
    }

    [Test]
    public void UpdateFloorVisibility_DisablesRenderer_WhenCameraBelowFloorTop()
    {
        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _controller!.UpdateFloorVisibility();

        var renderer = _floorGo!.GetComponent<MeshRenderer>();
        Assert.IsFalse(renderer.enabled, "floor should be invisible when camera is below it");
    }

    [Test]
    public void UpdateFloorVisibility_EnablesRenderer_WhenCameraAboveFloorTop()
    {
        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _controller!.UpdateFloorVisibility();

        var renderer = _floorGo!.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer.enabled, "floor should be visible when camera is above it");
    }

    [Test]
    public void UpdateFloorVisibility_DisablesCollider_WhenCameraBelowFloorTop()
    {
        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _controller!.UpdateFloorVisibility();

        var collider = _floorGo!.GetComponent<Collider>();
        Assert.IsFalse(collider.enabled, "floor collider should be disabled when camera is below it");
    }

    [Test]
    public void UpdateFloorVisibility_EnablesCollider_WhenCameraAboveFloorTop()
    {
        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _controller!.UpdateFloorVisibility();

        var collider = _floorGo!.GetComponent<Collider>();
        Assert.IsTrue(collider.enabled, "floor collider should be enabled when camera is above it");
    }

    [Test]
    public void UpdateFloorVisibility_DoesNotThrow_WhenFloorIsNull()
    {
        Object.DestroyImmediate(_floorGo!);
        _floorGo = null;

        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _controller!.UpdateFloorVisibility();
    }

    [Test]
    public void UpdateFloorVisibility_DoesNotThrow_WhenCameraIsNull()
    {
        Object.DestroyImmediate(_cameraGo!);
        _cameraGo = null;

        _controller!.UpdateFloorVisibility();
    }

    [Test]
    public void UpdateCameraPosition_DoesNotThrow_WhenCameraIsNull()
    {
        Object.DestroyImmediate(_cameraGo!);
        _cameraGo = null;

        _controller!.UpdateCameraPosition();
    }

    // ── Скорость клавиатурного управления ─────────────────────────────

    [Test]
    public void ApplyWASDMovement_MovesAtQuarterMultiplier()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 2f
        });

        float dt = 1f;
        _controller!.ApplyWASDMovement(Vector2.up, dt); // W

        var target = GetTarget();
        float expectedZ = 3f * 2f * 0.25f * dt; // _moveSpeed * _distance * 0.25f * dt
        Assert.AreEqual(expectedZ, target.z, 1e-4f, "WASD speed multiplier should be 0.25 (half of previous 0.5)");
        Assert.AreEqual(0f, target.x, 1e-4f);
    }

    [Test]
    public void ApplyArrowOrbit_RotatesAtHalvedSpeed()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        float dt = 1f;
        _controller!.ApplyArrowOrbit(Vector2.right, dt); // Right arrow

        var state = _controller!.GetState();
        Assert.AreEqual(45f * dt, state.angleY, 1e-4f,
            "keyboard orbit speed should be 45 deg/s (half of previous 90)");
    }

    [Test]
    public void ApplyZoomDelta_ZoomsAtHalvedSpeed()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyZoomDelta(1f); // Minus key (zoom in)

        var state = _controller!.GetState();
        float expectedDistance = 5f + 1f * 1f * 5f * 0.1f; // new multiplier 0.1 (half of previous 0.2)
        Assert.AreEqual(expectedDistance, state.distance, 1e-4f,
            "plus/minus zoom multiplier should be 0.1 (half of previous 0.2)");
    }

    [Test]
    public void IsInputField_ReturnsTrue_ForSelectedInputField()
    {
        var inputGo = new GameObject("InputField");
        inputGo.AddComponent<TMP_InputField>();

        Assert.IsTrue(CameraController.IsInputField(inputGo),
            "selected GameObject with InputField should be detected as typing");

        Object.DestroyImmediate(inputGo);
    }

    [Test]
    public void IsInputField_ReturnsFalse_ForNonInputFieldOrNull()
    {
        var plainGo = new GameObject("Plain");
        Assert.IsFalse(CameraController.IsInputField(plainGo),
            "GameObject without InputField should not report typing");
        Assert.IsFalse(CameraController.IsInputField(null!),
            "null selected object should not report typing");
        Object.DestroyImmediate(plainGo);
    }

    [Test]
    public void IsTypingInInputField_ReturnsFalse_WhenNoEventSystem()
    {
        Assert.IsFalse(CameraController.IsTypingInInputField(), "no EventSystem should not report typing");
    }

    [Test]
    public void ApplyWASDMovement_DoesNothing_WhenInputIsZero()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 1f, targetY = 2f, targetZ = 3f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyWASDMovement(Vector2.zero, 1f);

        var target = GetTarget();
        Assert.AreEqual(new Vector3(1f, 2f, 3f), target);
    }

    private Vector3 GetTarget()
    {
        var state = _controller!.GetState();
        return new Vector3(state.targetX, state.targetY, state.targetZ);
    }
}
