using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class RunRoomSideTests
{
    private static BoxMm Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
        new BoxMm(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

    private static readonly BoxMm WallAlongX = Box(0, 0, -100, 4000, 2700, 100);

    private static readonly BoxMm WallAlongZ = Box(-100, 0, 0, 100, 2700, 3000);

    private static bool Resolve(BoxMm wall, int axis, string? word, IReadOnlyList<BoxMm> floors, out bool onMax, out string problem) =>
        RunRoomSide.TryResolve("Wall", wall, axis, word, floors, out onMax, out problem);

    [Test]
    public void RunAxisOf_IsTheLongerHorizontalSide()
    {
        Assert.AreEqual(0, RunWall.RunAxisOf(WallAlongX));
        Assert.AreEqual(2, RunWall.RunAxisOf(WallAlongZ));
    }

    [Test]
    public void TryResolve_TheFloorStretchesToOneSide_ThatSideIsTheRoom()
    {
        var floors = new[] { Box(0, -100, -100, 4000, 0, 3000) };

        Assert.IsTrue(Resolve(WallAlongX, 0, null, floors, out bool onMax, out _));
        Assert.IsTrue(onMax, "пол от стены в сторону z+");

        var mirrored = new[] { Box(0, -100, -3000, 4000, 0, 100) };
        Assert.IsTrue(Resolve(WallAlongX, 0, null, mirrored, out onMax, out _));
        Assert.IsFalse(onMax, "пол в сторону z-: комната по ту сторону");
    }

    [Test]
    public void TryResolve_AWallAlongZ_ReadsTheXSide()
    {
        var floors = new[] { Box(-3000, -100, 0, 100, 0, 3000) };

        Assert.IsTrue(Resolve(WallAlongZ, 2, null, floors, out bool onMax, out _));
        Assert.IsFalse(onMax, "пол слева, в сторону x-");
    }

    [Test]
    public void TryResolve_AFloorThatDoesNotTouchTheWallsSpan_DoesNotVote()
    {
        var floors = new[] { Box(5000, -100, -100, 9000, 0, 3000) };

        Assert.IsFalse(Resolve(WallAlongX, 0, null, floors, out _, out string problem));
        StringAssert.Contains("room_side", problem, "отказ называет параметр, которым выбирают сторону");
    }

    [Test]
    public void TryResolve_FloorOnBothSides_IsAmbiguous_AndSaysWhichWordsToSend()
    {
        var floors = new[] { Box(0, -100, -3000, 4000, 0, 3000) };

        Assert.IsFalse(Resolve(WallAlongX, 0, null, floors, out _, out string problem));
        StringAssert.Contains("'front' or 'back'", problem);
    }

    [Test]
    public void TryResolve_NoFloorAtAll_AsksForTheWord()
    {
        Assert.IsFalse(Resolve(WallAlongX, 0, null, new BoxMm[0], out _, out string problem));
        StringAssert.Contains("cannot tell", problem);
    }

    [Test]
    public void TryResolve_TheWord_WinsOverTheFloor()
    {
        var floors = new[] { Box(0, -100, -100, 4000, 0, 3000) };

        Assert.IsTrue(Resolve(WallAlongX, 0, "back", floors, out bool onMax, out _));
        Assert.IsFalse(onMax, "явное слово пользователя сильнее догадки по полу");
    }

    [Test]
    public void TryResolve_AWordOfTheOtherAxis_IsRefusedWithTheRightPair()
    {
        Assert.IsFalse(Resolve(WallAlongX, 0, "left", new BoxMm[0], out _, out string problem));
        StringAssert.Contains("front|back", problem);

        Assert.IsFalse(Resolve(WallAlongZ, 2, "front", new BoxMm[0], out _, out problem));
        StringAssert.Contains("left|right", problem);
    }

    [Test]
    public void RunWall_RoomFace_NamesTheFaceThatLooksIntoTheRoom()
    {
        Assert.AreEqual("front", new RunWall("W", WallAlongX, 0, true).RoomFace);
        Assert.AreEqual("back", new RunWall("W", WallAlongX, 0, false).RoomFace);
        Assert.AreEqual("right", new RunWall("W", WallAlongZ, 2, true).RoomFace);
        Assert.AreEqual("left", new RunWall("W", WallAlongZ, 2, false).RoomFace);
    }
}
