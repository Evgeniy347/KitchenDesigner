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
    public void PhotoDistance_RoundTrips_Independently()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 10f, angleY = 20f, distance = 4f, photoDistance = 12f
        });
        var s = _controller!.GetState();
        Assert.AreEqual(4f, s.distance, 0.001f, "обычный зум сохранён");
        Assert.AreEqual(12f, s.photoDistance, 0.001f, "зум фоторежима сохранён отдельно");
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
    public void UpdateFloorVisibility_DisablesRenderer_WhenCameraBelowFloorTop_AndLookingUp()
    {
        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _cameraGo!.transform.forward = new Vector3(0, 0.5f, 0.866f).normalized;
        _controller!.UpdateFloorVisibility();

        var renderer = _floorGo!.GetComponent<MeshRenderer>();
        Assert.IsFalse(renderer.enabled, "floor should be invisible when camera is below it and looking up");
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
    public void UpdateFloorVisibility_DisablesCollider_WhenCameraBelowFloorTop_AndLookingUp()
    {
        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _cameraGo!.transform.forward = new Vector3(0, 0.5f, 0.866f).normalized;
        _controller!.UpdateFloorVisibility();

        var collider = _floorGo!.GetComponent<Collider>();
        Assert.IsFalse(collider.enabled, "floor collider should be disabled when camera is below it and looking up");
    }

    [Test]
    public void UpdateFloorVisibility_EnablesCollider_WhenCameraAboveFloorTop()
    {
        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _controller!.UpdateFloorVisibility();

        var collider = _floorGo!.GetComponent<Collider>();
        Assert.IsTrue(collider.enabled, "floor collider should be enabled when camera is above it");
    }

    // ── Полигональный пол: два коллайдера (Box отключён, работает Mesh) ───

    /// <summary>Строит пол так же, как ElementFactory.CreateFloor + SetPolygonLocalMm:
    /// куб с BoxCollider, поверх — MeshCollider по контуру, Box отключается.</summary>
    private GameObject CreatePolygonFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "PolyFloor";
        var floor = go.AddComponent<FloorElement>();
        floor.DimensionsMM = new Vector3Int(3000, 100, 3000);
        go.transform.position = new Vector3(0, -0.05f, 0);
        floor.SetPolygonLocalMm(new[]
        {
            new Vector2Int(-1500, -1500), new Vector2Int(1500, -1500),
            new Vector2Int(1500, 1500), new Vector2Int(-1500, 1500)
        });
        return go;
    }

    [Test]
    public void UpdateFloorVisibility_DisablesMeshCollider_OfPolygonFloor_WhenCameraBelowAndLookingUp()
    {
        var polyGo = CreatePolygonFloor();
        _controller!.AssignTestFloor(polyGo);

        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _cameraGo!.transform.forward = new Vector3(0, 0.5f, 0.866f).normalized;
        _controller!.UpdateFloorVisibility();

        var mesh = polyGo.GetComponent<MeshCollider>();
        Assert.IsFalse(mesh.enabled,
            "меш-коллайдер полигонального пола должен отключаться, иначе клик снизу не проходит сквозь скрытый пол");

        Object.DestroyImmediate(polyGo);
    }

    [Test]
    public void UpdateFloorVisibility_KeepsBoxColliderDisabled_OnPolygonFloor()
    {
        var polyGo = CreatePolygonFloor();
        _controller!.AssignTestFloor(polyGo);

        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _controller!.UpdateFloorVisibility();

        var box = polyGo.GetComponent<BoxCollider>();
        Assert.IsFalse(box.enabled,
            "прямоугольный BoxCollider полигонального пола выключен навсегда — камера не должна его включать");
        Assert.IsTrue(polyGo.GetComponent<MeshCollider>().enabled,
            "меш-коллайдер снова включён, когда камера выше пола");

        Object.DestroyImmediate(polyGo);
    }

    [Test]
    public void UpdateFloorVisibility_KeepsFloorVisible_WhenCameraBelowFloorButLookingDown()
    {
        _cameraGo!.transform.position = new Vector3(0, -1f, 0);
        _cameraGo!.transform.forward = new Vector3(0, -0.5f, 0.866f).normalized;
        _controller!.UpdateFloorVisibility();

        var renderer = _floorGo!.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer.enabled, "floor should stay visible when camera is below but looking down");
    }

    [Test]
    public void UpdateFloorVisibility_KeepsFloorVisible_WhenCameraAboveAndLookingUp()
    {
        _cameraGo!.transform.position = new Vector3(0, 10f, 0);
        _cameraGo!.transform.forward = new Vector3(0, 0.5f, 0.866f).normalized;
        _controller!.UpdateFloorVisibility();

        var renderer = _floorGo!.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer.enabled, "floor should stay visible when camera is above floor regardless of look direction");
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
    public void ApplyWASDMovement_FirstSecond_MovesAtBaseSpeed_IndependentOfZoom()
    {
        // Одна и та же секунда удержания при разном зуме даёт одно и то же смещение.
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 2f
        });
        _controller!.ApplyWASDMovement(Vector2.up, 1f); // W
        float near = GetTarget().z;

        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 18f
        });
        _controller!.ApplyWASDMovement(Vector2.zero, 0.1f); // сброс разгона
        _controller!.ApplyWASDMovement(Vector2.up, 1f);
        float far = GetTarget().z;

        Assert.AreEqual(0.6f, near, 1e-4f, "первая секунда — базовая скорость 0.6 м/с");
        Assert.AreEqual(near, far, 1e-4f, "скорость WASD не должна зависеть от зума");
    }

    [Test]
    public void WasdHoldMultiplier_WithoutShift_AlwaysReturnsOne()
    {
        Assert.AreEqual(1f, CameraController.WasdHoldMultiplier(0f, false), 1e-4f, "старт — ×1");
        Assert.AreEqual(1f, CameraController.WasdHoldMultiplier(1f, false), 1e-4f, "через 1 с — всё ещё ×1");
        Assert.AreEqual(1f, CameraController.WasdHoldMultiplier(5f, false), 1e-4f, "через 5 с — ×1, разгона нет");
        Assert.AreEqual(1f, CameraController.WasdHoldMultiplier(30f, false), 1e-4f, "долгое удержание — ×1");
    }

    [Test]
    public void WasdHoldMultiplier_WithShift_ReachesMaxInOneSecond()
    {
        Assert.AreEqual(1f, CameraController.WasdHoldMultiplier(0f, true), 1e-4f);
        Assert.AreEqual(10.5f, CameraController.WasdHoldMultiplier(0.5f, true), 1e-3f,
            "с Shift разгон идёт сразу, без паузы в первую секунду");
        Assert.AreEqual(20f, CameraController.WasdHoldMultiplier(1f, true), 1e-4f);
    }

    [Test]
    public void ApplyWASDMovement_ResetsRamp_WhenKeysReleased()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        // Без Shift: ускорения нет, каждый шаг — базовая скорость 0.6 м/с.
        _controller!.ApplyWASDMovement(Vector2.up, 1f);
        float after1 = GetTarget().z;
        _controller!.ApplyWASDMovement(Vector2.up, 1f);
        float after2 = GetTarget().z;
        Assert.AreEqual(0.6f, after1, 1e-4f, "без Shift — всегда базовая скорость");
        Assert.AreEqual(0.6f, after2 - after1, 1e-4f, "второй шаг — та же скорость, разгона нет");

        // С Shift: разгон работает, сброс по отпусканию.
        _controller!.ApplyWASDMovement(Vector2.zero, 0.1f);
        for (int i = 0; i < 2; i++) _controller!.ApplyWASDMovement(Vector2.up, 1f, shift: true);
        _controller!.ApplyWASDMovement(Vector2.zero, 0.1f);

        float before = GetTarget().z;
        _controller!.ApplyWASDMovement(Vector2.up, 1f, shift: true);
        Assert.AreEqual(0.6f, GetTarget().z - before, 1e-4f,
            "после отпускания разгон с Shift начинается заново");
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
    public void ApplyZoomDelta_MovesCameraForward_WithoutChangingDistance()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyZoomDelta(-1f); // клавиша «+» — приблизиться
        _controller!.UpdateCameraPosition();

        var state = _controller!.GetState();
        Assert.AreEqual(5f, state.distance, 1e-4f, "зум больше не меняет радиус орбиты");
        Assert.AreEqual(0.3f, state.targetZ, 1e-4f, "камера сместилась вперёд на шаг зума");
        Assert.AreEqual(-4.7f, _cameraGo!.transform.position.z, 1e-3f,
            "позиция камеры сдвинулась вперёд на тот же шаг");
    }

    [Test]
    public void MoveForward_FollowsLookDirection_IncludingPitch()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 90f, angleY = 0f, distance = 5f   // смотрим строго вниз
        });

        _controller!.MoveForward(1f);

        var t = GetTarget();
        Assert.AreEqual(-1f, t.y, 1e-3f, "взгляд вниз — смещение вперёд идёт вниз");
        Assert.AreEqual(0f, t.z, 1e-3f);
    }

    // ── ПКМ: поворот на месте ─────────────────────────────────────────

    [Test]
    public void ApplyOrbit_KeepsCameraPosition_AndTurnsView()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });
        _controller!.UpdateCameraPosition();
        Vector3 before = _cameraGo!.transform.position;

        _controller!.ApplyOrbit(90f, 0f);
        _controller!.UpdateCameraPosition();

        Assert.AreEqual(before.x, _cameraGo!.transform.position.x, 1e-3f, "камера осталась на месте");
        Assert.AreEqual(before.y, _cameraGo!.transform.position.y, 1e-3f);
        Assert.AreEqual(before.z, _cameraGo!.transform.position.z, 1e-3f);

        var t = GetTarget();
        Assert.AreEqual(5f, t.x, 1e-3f, "точка взгляда переехала вправо от камеры");
        Assert.AreEqual(-5f, t.z, 1e-3f);
    }

    [Test]
    public void ApplyOrbit_ClampsPitch()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyOrbit(0f, 200f);
        Assert.AreEqual(89f, _controller!.GetState().angleX, 1e-4f);
    }

    // ── Плавный фокус ─────────────────────────────────────────────────

    [Test]
    public void FocusOn_MovesTargetGradually_OverTwoSeconds()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.FocusOn(new Vector3(10f, 0f, 0f));
        Assert.IsTrue(_controller!.IsFocusing, "перелёт начался");
        Assert.AreEqual(0f, GetTarget().x, 1e-4f, "камера не прыгает мгновенно");

        _controller!.UpdateFocus(1f);
        float mid = GetTarget().x;
        Assert.Greater(mid, 0.5f, "за половину времени камера прошла заметную часть пути");
        Assert.Less(mid, 9.5f);
        Assert.IsTrue(_controller!.IsFocusing, "через 1 из 2 секунд перелёт ещё идёт");

        _controller!.UpdateFocus(1f);
        Assert.AreEqual(10f, GetTarget().x, 1e-3f, "через 2 секунды камера в точке фокуса");
        Assert.IsFalse(_controller!.IsFocusing, "перелёт завершён");
    }

    [Test]
    public void FocusOn_IsCancelled_ByManualMovement()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.FocusOn(new Vector3(10f, 0f, 0f));
        _controller!.ApplyWASDMovement(Vector2.up, 0.1f);
        Assert.IsFalse(_controller!.IsFocusing, "ручное управление прерывает перелёт");

        float z = GetTarget().z;
        _controller!.UpdateFocus(1f);
        Assert.AreEqual(0f, GetTarget().x, 1e-4f, "прерванный перелёт не продолжается");
        Assert.AreEqual(z, GetTarget().z, 1e-4f);
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

    // ── Плавный зум колесом мыши ──────────────────────────────────────

    [Test]
    public void ApplyScrollInput_DoesNotMoveCamera_Immediately()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyScrollInput(0.3f);
        Assert.AreEqual(0f, GetTarget().z, 1e-6f,
            "накопление не должно двигать камеру мгновенно");
    }

    [Test]
    public void UpdateScrollSmooth_MovesGradually_OneStepPartWay()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyScrollInput(0.3f);
        _controller!.UpdateScrollSmooth(0.08f);

        float z = GetTarget().z;
        Assert.Greater(z, 0.01f, "один шаг SmoothDamp должен сместить камеру");
        Assert.Less(z, 0.3f, "за один шаг камера не должна долететь до цели");
    }

    [Test]
    public void UpdateScrollSmooth_ConvergesToTarget()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyScrollInput(0.3f);

        for (int i = 0; i < 50; i++)
            _controller!.UpdateScrollSmooth(0.016f);

        Assert.AreEqual(0.3f, GetTarget().z, 1e-4f,
            "после 0.8 с камера должна сойтись к цели");
    }

    [Test]
    public void UpdateScrollSmooth_MultipleEvents_Stack()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyScrollInput(0.3f);
        _controller!.ApplyScrollInput(0.2f);
        _controller!.ApplyScrollInput(-0.1f);

        for (int i = 0; i < 50; i++)
            _controller!.UpdateScrollSmooth(0.016f);

        Assert.AreEqual(0.4f, GetTarget().z, 1e-4f,
            "несколько событий колеса должны суммироваться");
    }

    [Test]
    public void UpdateScrollSmooth_DoesNothing_AtRest()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.UpdateScrollSmooth(0.1f);
        Assert.AreEqual(0f, GetTarget().z, 1e-6f,
            "без событий колеса плавный зум не должен двигать камеру");
    }

    // ── Непрерывный зум клавишами +/− ─────────────────────────────────

    [Test]
    public void ApplyZoomMovement_MovesForward_AtWasdSpeed()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyZoomMovement(1f, 1f);

        Assert.AreEqual(0.6f, GetTarget().z, 1e-4f,
            "1 секунда '+' смещает камеру вперёд на 0.6 м (как WASD без Shift)");
    }

    [Test]
    public void ApplyZoomMovement_MovesBackward_AtWasdSpeed()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyZoomMovement(-1f, 1f);

        Assert.AreEqual(-0.6f, GetTarget().z, 1e-4f,
            "1 секунда '−' смещает камеру назад на 0.6 м");
    }

    [Test]
    public void ApplyZoomMovement_CancelsFocus()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.FocusOn(new Vector3(10f, 0f, 0f));
        Assert.IsTrue(_controller!.IsFocusing, "перелёт начался");

        _controller!.ApplyZoomMovement(1f, 0.1f);
        Assert.IsFalse(_controller!.IsFocusing,
            "непрерывный зум +/- должен прерывать перелёт");
    }

    [Test]
    public void ApplyZoomMovement_ResetsScrollSmooth()
    {
        _controller!.SetState(new CameraState
        {
            valid = true,
            targetX = 0f, targetY = 0f, targetZ = 0f,
            angleX = 0f, angleY = 0f, distance = 5f
        });

        _controller!.ApplyScrollInput(0.5f);
        _controller!.UpdateScrollSmooth(0.08f);
        float afterScroll = GetTarget().z;
        Assert.Greater(afterScroll, 0f, "скролл отодвинул камеру");

        _controller!.ApplyZoomMovement(1f, 0.1f);
        float afterZoom = GetTarget().z;
        Assert.Greater(afterZoom, afterScroll, "+/- сдвинул камеру дополнительно");

        float before = GetTarget().z;
        _controller!.UpdateScrollSmooth(0.1f);
        Assert.AreEqual(before, GetTarget().z, 1e-6f,
            "после +/- плавный зум колеса не должен применяться");
    }

    private Vector3 GetTarget()
    {
        var state = _controller!.GetState();
        return new Vector3(state.targetX, state.targetY, state.targetZ);
    }
}
