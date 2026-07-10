using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CameraStateTests
{
    private GameObject _go;

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
    public void CameraState_SurvivesProjectSerialize()
    {
        var data = new ProjectData
        {
            camera = new CameraState
            {
                valid = true,
                targetX = 3f, targetY = 1f, targetZ = 2f,
                angleX = 25f, angleY = 90f, distance = 6f
            }
        };

        var restored = SaveLoadManager.Deserialize(SaveLoadManager.Serialize(data));

        Assert.IsTrue(restored.camera.valid);
        Assert.AreEqual(3f, restored.camera.targetX, 0.0001f);
        Assert.AreEqual(90f, restored.camera.angleY, 0.0001f);
        Assert.AreEqual(6f, restored.camera.distance, 0.0001f);
    }

    [Test]
    public void CameraState_DefaultInvalid_NotApplied()
    {
        // Старый сейв без камеры → camera.valid=false (RestoreScene не трогает камеру).
        var data = new ProjectData();
        Assert.IsFalse(data.camera.valid);
    }
}
