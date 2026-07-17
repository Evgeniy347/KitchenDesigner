using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DoorSnapTests : SnapTestBase
{
    [Test]
    public void Door_AttachesToNearestWall()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorTest", new Vector3(0, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<KitchenElement>();
        PartRegistry.Register(wall);
        Assert.IsNotNull(wall);

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_Test", new Vector3(0, 1.0f, -1.5f));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        PartRegistry.Register(door);
        Assert.IsNotNull(door);

        door!.SnapToWall();

        Assert.AreEqual("Wall_DoorTest", door.AttachedWallName);
    }

    [Test]
    public void Door_CreatedWithoutWall_NoAttachment()
    {
        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_NoWall", Vector3.zero);
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        Assert.IsEmpty(door.AttachedWallName);
    }

    [Test]
    public void Door_SnapToWall_CentersAndInheritsThickness()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(200, 2500, 3000), "Wall_DoorThick", new Vector3(-1.5f, 1.25f, 0f));
        _spawned.Add(wallGo);

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_Align", new Vector3(-1.3f, 1.0f, 0.4f));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        door!.SnapToWall();

        Assert.AreEqual("Wall_DoorThick", door.AttachedWallName);
        Assert.AreEqual(-1.5f, door.transform.position.x, Tol, "дверь должна встать в середину толщины стены");
        Assert.AreEqual(1.0f, door.transform.position.y, Tol);
        Assert.AreEqual(0.4f, door.transform.position.z, Tol);
        Assert.AreEqual(200, door.DimensionsMM.z, "глубина двери должна наследовать толщину стены");
    }

    [Test]
    public void Door_Height_ClampedToWallHeight()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2000, 3000), "Wall_DoorH", new Vector3(0, 1.0f, 0));
        _spawned.Add(wallGo);

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 3000, 100), "Door_H", new Vector3(0, 1.0f, 0));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        door!.SnapToWall();

        Assert.AreEqual("Wall_DoorH", door.AttachedWallName);
        Assert.AreEqual(2000, door.DimensionsMM.y,
            "высота двери должна быть обрезана до высоты стены");
    }

    [Test]
    public void Door_YPosition_ClampedToWallBounds()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorPos", new Vector3(0, 1.25f, 0));
        _spawned.Add(wallGo);

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 500, 100), "Door_Pos", new Vector3(0, 2.5f, 0));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        door!.SnapToWall();

        float toU = AppConstants.MM_TO_UNITS;
        float wallTop = 1.25f + 2500 * toU * 0.5f;
        float doorHalfH = door.DimensionsMM.y * toU * 0.5f;
        float expectedMaxY = wallTop - doorHalfH;

        Assert.LessOrEqual(door.transform.position.y, expectedMaxY + 1e-5f,
            "центр двери не должен быть выше верхней границы стены");
    }

    [Test]
    public void Door_Open_FrameStays_SashRotates()
    {
        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_Open", new Vector3(0f, 1.0f, 0f));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        var pos0 = doorGo.transform.position;
        var rot0 = doorGo.transform.rotation;
        var frame = doorGo.transform.Find("_Static/FrameLeft");
        var framePos0 = frame!.position;

        door!.SetOpen(true);
        door.StepDoor(1f);

        Assert.AreEqual(0f, (doorGo.transform.position - pos0).magnitude, 1e-6f,
            "корень двери не должен двигаться при открывании");
        Assert.AreEqual(0f, Quaternion.Angle(doorGo.transform.rotation, rot0), 1e-4f,
            "корень двери не должен поворачиваться при открывании");
        Assert.AreEqual(0f, (frame.position - framePos0).magnitude, 1e-6f,
            "коробка (рама) должна оставаться на месте");

        var sash = doorGo.transform.Find("_Sash");
        Assert.IsNotNull(sash, "Sash group must exist");
        Assert.AreEqual(90f, Quaternion.Angle(sash!.localRotation, Quaternion.identity), 0.5f,
            "створка должна распахнуться на 90°");
    }

    [Test]
    public void Door_RestoredFromSave_RegistersInWall()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 7240), "DoorWall", new Vector3(-1.635f, 1.35f, 0f));
        _spawned.Add(wallGo);
        var wall = wallGo.GetComponent<Wall>();

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_Loaded", new Vector3(-1.635f, 1.35f, 0f));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        door!.AttachedWallName = "DoorWall";

        Assert.IsFalse(wall!.HasDoor(door), "до снапа стена дверь не знает");

        door.SnapToWall();

        Assert.IsTrue(wall.HasDoor(door), "снап должен зарегистрировать дверь в стене");
        Assert.Greater(wallGo.GetComponent<MeshFilter>()!.sharedMesh!.vertexCount, 24,
            "в мешe стены должен появиться вырез");
    }

    [Test]
    public void Door_WithinWall_NotClamped()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_DoorOK", new Vector3(0, 1.25f, 0));
        _spawned.Add(wallGo);

        var doorGo = ElementFactory.CreateDoor(
            new Vector3Int(900, 2000, 100), "Door_OK", new Vector3(0, 1.0f, 0));
        _spawned.Add(doorGo);
        var door = doorGo.GetComponent<DoorElement>();
        Assert.IsNotNull(door);

        door!.SnapToWall();

        Assert.AreEqual(2000, door.DimensionsMM.y,
            "высота двери, умещающейся в стену, не должна меняться");
        Assert.AreEqual(1.0f, door.transform.position.y, Tol,
            "позиция двери, умещающейся в стену, не должна меняться");
    }
}
