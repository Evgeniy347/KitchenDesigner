using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallCutawayTests
{
    [Test]
    public void NearWall_BetweenCameraAndCenter_Lowers()
    {
        // Камера смотрит вдоль +Z; ближняя стена со стороны камеры (-Z от центра).
        var nearWall = new Vector3(0, 1.25f, -2f);
        Assert.IsTrue(WallCutaway.ShouldLower(nearWall, Vector3.zero, Vector3.forward));
    }

    [Test]
    public void FarWall_BeyondCenter_StaysUp()
    {
        var farWall = new Vector3(0, 1.25f, 2f);
        Assert.IsFalse(WallCutaway.ShouldLower(farWall, Vector3.zero, Vector3.forward));
    }

    [Test]
    public void SideWall_Perpendicular_StaysUp()
    {
        var sideWall = new Vector3(2f, 1.25f, 0f);
        Assert.IsFalse(WallCutaway.ShouldLower(sideWall, Vector3.zero, Vector3.forward));
    }

    [Test]
    public void Partition_OnCameraSide_Lowers()
    {
        // Перегородка посреди комнаты, но на стороне камеры от центра.
        var camForward = new Vector3(0, -0.5f, 1f); // взгляд вниз-вперёд
        var partition = new Vector3(0, 1.25f, -0.5f);
        Assert.IsTrue(WallCutaway.ShouldLower(partition, Vector3.zero, camForward));
    }
}
