using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Tools;

public class CameraPhotoPoseTests
{
    private GameObject? _cameraGo;
    private GameObject? _floorGo;
    private CameraController? _controller;

    [SetUp]
    public void SetUp()
    {
        EditModeManager.SetMode(EditMode.Normal);
        PartRegistry.Clear();

        _cameraGo = new GameObject("TestCamera");
        _cameraGo.tag = "MainCamera";
        _cameraGo.AddComponent<Camera>();

        _floorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _floorGo.name = "TestFloor";
        _floorGo.tag = "Floor";
        _floorGo.transform.localScale = new Vector3(3f, 0.018f, 3f);
        _floorGo.transform.position = new Vector3(0f, -0.009f, 0f);

        var controllerGo = new GameObject("CameraController");
        _controller = controllerGo.AddComponent<CameraController>();
        _controller.AssignTestCamera(_cameraGo.GetComponent<Camera>());
        _controller.AssignTestFloor(_floorGo);
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.SetMode(EditMode.Normal);
        EyedropperMode.Reset();
        Object.DestroyImmediate(_controller!.gameObject);
        Object.DestroyImmediate(_floorGo!);
        Object.DestroyImmediate(_cameraGo!);
        PartRegistry.Clear();
    }

    private static CameraState PoseWithBothCameras() => new CameraState
    {
        valid = true,
        targetX = 1f, targetY = 0f, targetZ = 0f,
        angleX = 10f, angleY = 20f, distance = 5f,
        photoDistance = 8f,
        photoTargetX = 5f, photoTargetY = 0f, photoTargetZ = 0f,
        photoAngleX = 40f, photoAngleY = 50f,
    };

    [Test]
    public void PhotoPose_MovesIndependentlyOfTheWorkingOne()
    {
        Assume.That(KitchenSettings.Instance.WasdSpeed, Is.GreaterThan(0f));
        Assume.That(KitchenSettings.Instance.ArrowSpeed, Is.GreaterThan(0f));

        _controller!.SetState(PoseWithBothCameras());

        EditModeManager.SetMode(EditMode.Photo);
        try
        {
            _controller.ApplyWASDMovement(new Vector2(1f, 0f), dt: 1f);
            _controller.ApplyArrowOrbit(new Vector2(1f, 0f), dt: 1f);
        }
        finally
        {
            EditModeManager.SetMode(EditMode.Normal);
        }

        var s = _controller.GetState();
        Assert.AreEqual(1f, s.targetX, 1e-4f,
            "езда камерой в фоторежиме не должна трогать обычную позицию: у режимов свои ракурсы");
        Assert.AreEqual(20f, s.angleY, 1e-4f, "и обычные углы тоже остаются на месте");
        Assert.AreNotEqual(5f, s.photoTargetX, "а фото-позиция обязана была сдвинуться");
        Assert.AreNotEqual(50f, s.photoAngleY, "и фото-углы вместе с ней");
    }

    [Test]
    public void PhotoPose_SurvivesTheRoundTripThroughTheProject()
    {
        _controller!.SetState(PoseWithBothCameras());
        var s = _controller.GetState();

        Assert.AreEqual(5f, s.photoTargetX, 1e-4f);
        Assert.AreEqual(40f, s.photoAngleX, 1e-4f);
        Assert.AreEqual(50f, s.photoAngleY, 1e-4f);
        Assert.AreEqual(8f, s.photoDistance, 1e-4f,
            "фоторежим держит собственный зум и сохраняет его в проект");
        Assert.AreEqual(5f, s.distance, 1e-4f, "обычный зум — отдельное число");
    }

    [Test]
    public void SetState_LegacyProjectWithoutPhotoPose_OpensWhereTheWorkingCameraStands()
    {
        var legacy = new CameraState
        {
            valid = true,
            targetX = 3f, targetY = 1f, targetZ = -2f,
            angleX = 15f, angleY = 25f, distance = 6f,
        };
        Assume.That(CameraController.HasSavedPhotoTarget(legacy), Is.False);
        Assume.That(CameraController.HasSavedPhotoAngles(legacy), Is.False);

        _controller!.SetState(legacy);
        var s = _controller.GetState();

        Assert.AreEqual(3f, s.photoTargetX, 1e-4f,
            "старый проект без фото-позиции обязан открыться там же, где обычная камера, а не в нуле сцены");
        Assert.AreEqual(-2f, s.photoTargetZ, 1e-4f);
        Assert.AreEqual(15f, s.photoAngleX, 1e-4f, "фото-углы старого проекта берутся у обычных");
        Assert.AreEqual(25f, s.photoAngleY, 1e-4f);
    }

    [Test]
    public void BasePlate_GivesWayToUserFloors_SoTheirTopPlanesDoNotFlicker()
    {
        Assert.IsTrue(CameraController.BasePlateVisibleWith(0),
            "без пользовательских полов опорная плита — единственный пол в кадре");
        Assert.IsFalse(CameraController.BasePlateVisibleWith(1),
            "с пользовательским полом обе плоскости лежат на y=0 и мерцают: рендер плиты гасим");
        Assert.IsFalse(CameraController.BasePlateVisibleWith(5));
    }

    [Test]
    public void Floor_HidesOnlyFromBelowAndOnlyOutsidePhotoMode()
    {
        Assert.IsTrue(CameraController.FloorHiddenFromCamera(cameraBelowTop: true, lookingUp: true),
            "снизу вверх пол закрывает вид — прячем");
        Assert.IsFalse(CameraController.FloorHiddenFromCamera(cameraBelowTop: true, lookingUp: false),
            "камера ниже, но смотрит вниз — прятать нечего");
        Assert.IsFalse(CameraController.FloorHiddenFromCamera(cameraBelowTop: false, lookingUp: true));

        EditModeManager.SetMode(EditMode.Photo);
        try
        {
            Assert.IsFalse(CameraController.FloorHiddenFromCamera(cameraBelowTop: true, lookingUp: true),
                "в фоторежиме сцена цельная: дыра в полу испортила бы кадр");
        }
        finally
        {
            EditModeManager.SetMode(EditMode.Normal);
        }
    }

    [Test]
    public void ResolveRmbClick_InEyedropperMode_PicksTheDecor_InsteadOfOpeningAMenu()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "PickBoard", Vector3.zero);
        try
        {
            var e = go.GetComponent<KitchenElement>()!;
            MaterialManager.ApplyById(e, "oak");
            Assume.That(MaterialManager.MaterialIdOf(e, MaterialSlot.Base), Is.EqualTo("oak"),
                "декор должен реально примениться, иначе пипетке нечего забирать");
            Physics.SyncTransforms();

            EyedropperMode.Reset();
            EyedropperMode.SetActive(true);

            _controller!.ResolveRmbClick(new Ray(new Vector3(0f, 2f, 0f), Vector3.down), shiftHeld: false);

            Assert.AreEqual("oak", EyedropperMode.PickedMaterialId,
                "ПКМ в режиме пипетки — это забор декора, а не контекстное меню; маршрут идёт через "
                + "камеру, потому что отличить клик от орбиты умеет только она");
        }
        finally
        {
            EyedropperMode.Reset();
            Object.DestroyImmediate(go);
        }
    }
}
