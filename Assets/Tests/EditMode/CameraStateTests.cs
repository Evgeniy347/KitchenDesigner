using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CameraStateTests
{
    private GameObject? _go;

    [TearDown]
    public void Teardown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void GetState_SetState_RoundTrips()
    {
        _go = new GameObject("Cam");
        var cc = _go.AddComponent<CameraController>();

        cc.SetState(new CameraState
        {
            valid = true,
            targetX = 1.5f, targetY = 0.5f, targetZ = -2f,
            angleX = 42f, angleY = 17f, distance = 8f
        });

        var s = cc.GetState();
        Assert.IsTrue(s.valid);
        Assert.AreEqual(1.5f, s.targetX, 0.0001f);
        Assert.AreEqual(0.5f, s.targetY, 0.0001f);
        Assert.AreEqual(-2f, s.targetZ, 0.0001f);
        Assert.AreEqual(42f, s.angleX, 0.0001f);
        Assert.AreEqual(17f, s.angleY, 0.0001f);
        Assert.AreEqual(8f, s.distance, 0.0001f);
    }

    [Test]
    public void GetState_SetState_PhotoFieldsRoundTrip()
    {
        _go = new GameObject("Cam");
        var cc = _go.AddComponent<CameraController>();

        cc.SetState(new CameraState
        {
            valid = true,
            targetX = 1f, targetY = 2f, targetZ = 3f,
            angleX = 30f, angleY = 45f, distance = 5f,
            photoDistance = 10f,
            photoTargetX = 4f, photoTargetY = 5f, photoTargetZ = 6f,
            photoAngleX = 60f, photoAngleY = 90f
        });

        var s = cc.GetState();
        Assert.IsTrue(s.valid);
        Assert.AreEqual(10f, s.photoDistance, 0.0001f);
        Assert.AreEqual(4f, s.photoTargetX, 0.0001f);
        Assert.AreEqual(5f, s.photoTargetY, 0.0001f);
        Assert.AreEqual(6f, s.photoTargetZ, 0.0001f);
        Assert.AreEqual(60f, s.photoAngleX, 0.0001f);
        Assert.AreEqual(90f, s.photoAngleY, 0.0001f);
    }

    [Test]
    public void PhotoState_IndependentFromNormal()
    {
        _go = new GameObject("Cam");
        var cc = _go.AddComponent<CameraController>();

        cc.SetState(new CameraState
        {
            valid = true,
            targetX = 1f, targetY = 1f, targetZ = 1f,
            angleX = 10f, angleY = 20f, distance = 3f,
            photoDistance = 12f,
            photoTargetX = 7f, photoTargetY = 8f, photoTargetZ = 9f,
            photoAngleX = 30f, photoAngleY = 40f
        });

        var s = cc.GetState();
        Assert.AreEqual(1f, s.targetX, 0.0001f);      // обычное не тронуто
        Assert.AreEqual(3f, s.distance, 0.0001f);
        Assert.AreEqual(7f, s.photoTargetX, 0.0001f);  // фото — своё
        Assert.AreEqual(12f, s.photoDistance, 0.0001f);
        Assert.AreEqual(30f, s.photoAngleX, 0.0001f);  // фото-угол независим
        Assert.AreEqual(10f, s.angleX, 0.0001f);       // обычный угол независим
    }

    [Test]
    public void CameraState_SurvivesProjectSerialize()
    {
        var data = new ProjectData
        {
            camera = new CameraState
            {
                valid = true,
                targetX = 3f, targetY = 1f, targetZ = 2f,
                angleX = 25f, angleY = 90f, distance = 6f,
                photoDistance = 15f,
                photoTargetX = 8f, photoTargetY = 9f, photoTargetZ = 10f,
                photoAngleX = 50f, photoAngleY = 70f
            }
        };

        var restored = SaveLoadManager.Deserialize(SaveLoadManager.Serialize(data));

        Assert.IsTrue(restored!.camera.valid);
        Assert.AreEqual(3f, restored!.camera.targetX, 0.0001f);
        Assert.AreEqual(90f, restored!.camera.angleY, 0.0001f);
        Assert.AreEqual(6f, restored!.camera.distance, 0.0001f);
        Assert.AreEqual(15f, restored!.camera.photoDistance, 0.0001f);
        Assert.AreEqual(8f, restored!.camera.photoTargetX, 0.0001f);
        Assert.AreEqual(50f, restored!.camera.photoAngleX, 0.0001f);
    }

    [Test]
    public void CameraState_OldSave_ZeroPhotoFields_FallsBack()
    {
        // Старый сейв: photo-поля = 0 → фото-состояние наследует обычное.
        _go = new GameObject("Cam");
        var cc = _go.AddComponent<CameraController>();

        cc.SetState(new CameraState
        {
            valid = true,
            targetX = 5f, targetY = 2f, targetZ = 3f,
            angleX = 15f, angleY = 25f, distance = 7f
            // photo-поля по умолчанию 0
        });

        var s = cc.GetState();
        // Нулевые photoTarget/photoAngle → фото унаследовало обычные значения.
        Assert.AreEqual(5f, s.photoTargetX, 0.0001f);
        Assert.AreEqual(2f, s.photoTargetY, 0.0001f);
        Assert.AreEqual(3f, s.photoTargetZ, 0.0001f);
        Assert.AreEqual(15f, s.photoAngleX, 0.0001f);
        Assert.AreEqual(25f, s.photoAngleY, 0.0001f);
    }
}
