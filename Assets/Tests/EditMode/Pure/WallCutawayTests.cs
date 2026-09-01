using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallCutawayTests
{
    [Test]
    public void NearWall_BetweenCameraAndCenter_Lowers()
    {
        var nearWall = new Vector3(0, 1.25f, -2f);
        Assert.IsTrue(WallCutaway.ShouldLower(nearWall, Vector3.zero, Vector3.forward),
            "стена на стороне камеры смещена ПРОТИВ направления взгляда от центра сцены");
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
        var camForwardLookingDown = new Vector3(0, -0.5f, 1f);
        var partition = new Vector3(0, 1.25f, -0.5f);
        Assert.IsTrue(WallCutaway.ShouldLower(partition, Vector3.zero, camForwardLookingDown),
            "наклон взгляда вниз не должен ничего решать: вертикаль отбрасывается, "
            + "решает только горизонтальная проекция");
    }

    [Test]
    public void WallHeight_DoesNotDecideAnything_OnlyTheHorizontalOffsetDoes()
    {
        var low = new Vector3(0f, 0.05f, -2f);
        var high = new Vector3(0f, 40f, -2f);
        Assert.AreEqual(
            WallCutaway.ShouldLower(low, Vector3.zero, Vector3.forward),
            WallCutaway.ShouldLower(high, Vector3.zero, Vector3.forward),
            "стена и её верхушка — одна и та же стена; если Y перестанут обнулять, "
            + "высокая перегородка начнёт вести себя иначе, чем низкая");
        Assert.IsTrue(WallCutaway.ShouldLower(high, Vector3.zero, Vector3.forward),
            "положительный контроль: обе стены обязаны ОПУСКАТЬСЯ, иначе равенство "
            + "выше зелено на «никогда не опускаем»");
    }

    [Test]
    public void CameraLookingStraightDown_LeavesEveryWallUp()
    {
        var nearWall = new Vector3(0, 1.25f, -2f);
        Assert.IsFalse(WallCutaway.ShouldLower(nearWall, Vector3.zero, Vector3.down),
            "у взгляда строго вниз нет горизонтальной проекции — решать нечем, "
            + "и ответ обязан быть «не опускаем»");
    }

    [Test]
    public void WallExactlyAtSceneCenter_StaysUp()
    {
        Assert.IsFalse(WallCutaway.ShouldLower(new Vector3(0f, 1.25f, 0f), Vector3.zero, Vector3.forward),
            "у стены ровно в центре нет стороны — решать нечем, ответ «не опускаем»");
    }
}
