using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallFloorPositionTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Wall_CreatedAtHalfHeight_SitsOnFloor()
    {
        var dims = new Vector3Int(3170, 2700, 100);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var e = MakeWall("BackWall", dims, new Vector3(0, centerY, 3.67f));

        float bottomY = e.transform.position.y - e.transform.localScale.y * 0.5f;
        Assert.AreEqual(0f, bottomY, 0.001f, "wall bottom should be on floor (y=0)");
        Assert.AreEqual(centerY, e.transform.position.y, 0.001f, "wall center should be at half height");
    }

    [Test]
    public void Wall_PositionPreserved_AfterCreation()
    {
        var dims = new Vector3Int(100, 2700, 7240);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        float posX = 1.635f;
        float posZ = 0f;

        var e = MakeWall("RightWall", dims, new Vector3(posX, centerY, posZ));

        Assert.AreEqual(posX, e.transform.position.x, 0.001f, "X preserved");
        Assert.AreEqual(centerY, e.transform.position.y, 0.001f, "Y preserved (wall on floor)");
        Assert.AreEqual(posZ, e.transform.position.z, 0.001f, "Z preserved");
    }

    [Test]
    public void Wall_SetLowered_MovesCenterTo100mm()
    {
        var dims = new Vector3Int(3170, 2700, 100);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var e = MakeWall("Wall", dims, new Vector3(0, centerY, 0));
        var wall = e.GetComponent<Wall>();

        wall.SetLowered(true, 0.1f);

        float loweredCenter = 0f + 0.1f * 0.5f; // base at 0 + half lowered height
        Assert.AreEqual(loweredCenter, e.transform.position.y, 0.001f, "lowered wall center");
    }

    [Test]
    public void Wall_RestoreFull_ReturnsToOriginalHeight()
    {
        var dims = new Vector3Int(3170, 2700, 100);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var e = MakeWall("Wall", dims, new Vector3(0, centerY, 0));
        var wall = e.GetComponent<Wall>();

        wall.SetLowered(true, 0.1f);
        wall.RestoreFull();

        Assert.AreEqual(centerY, e.transform.position.y, 0.001f, "restored wall center");
        Assert.AreEqual(dims.y * AppConstants.MM_TO_UNITS, e.transform.localScale.y, 0.001f, "restored wall height");
    }

    [Test]
    public void Wall_FullPosition_ReturnsOriginalCenter_WhenLowered()
    {
        var dims = new Vector3Int(3170, 2700, 100);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var e = MakeWall("Wall", dims, new Vector3(0, centerY, 0));
        var wall = e.GetComponent<Wall>();

        wall.SetLowered(true, 0.1f);

        Vector3 fullPos = wall.FullPosition;
        Assert.AreEqual(centerY, fullPos.y, 0.001f, "FullPosition.y = original center");
        Assert.AreEqual(0f, e.transform.position.y, 0.05f, "transform.position.y is lowered near 0");
    }

    [Test]
    public void Wall_FullPosition_StaysCorrect_AfterMultipleLowerRestore()
    {
        var dims = new Vector3Int(3170, 2700, 100);
        float centerY = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
        var e = MakeWall("Wall", dims, new Vector3(0, centerY, 0));
        var wall = e.GetComponent<Wall>();

        wall.SetLowered(true, 0.1f);
        wall.RestoreFull();
        wall.SetLowered(true, 0.1f);
        wall.RestoreFull();

        Assert.AreEqual(centerY, wall.FullPosition.y, 0.001f, "FullPosition preserved after cycles");
    }

    [Test]
    public void RoomWalls_StandOnFloor_InternalDimensions()
    {
        // Simulate a room with internal 3170x7240mm, 100mm thick walls, 2700mm high
        int roomW = 3170, roomD = 7240, roomH = 2700, wallT = 100;
        float halfW = roomW * 0.5f * AppConstants.MM_TO_UNITS;
        float halfD = roomD * 0.5f * AppConstants.MM_TO_UNITS;
        float halfT = wallT * 0.5f * AppConstants.MM_TO_UNITS;
        float centerY = roomH * 0.5f * AppConstants.MM_TO_UNITS;

        var walls = new (string name, Vector3Int dims, Vector3 pos)[]
        {
            ("BackWall",  new Vector3Int(roomW, roomH, wallT), new Vector3(0, centerY, halfD + halfT)),
            ("LeftWall",  new Vector3Int(wallT, roomH, roomD), new Vector3(-(halfW + halfT), centerY, 0)),
            ("RightWall", new Vector3Int(wallT, roomH, roomD), new Vector3(halfW + halfT, centerY, 0)),
            ("FrontWall", new Vector3Int(roomW, roomH, wallT), new Vector3(0, centerY, -(halfD + halfT))),
        };

        foreach (var (name, dims, pos) in walls)
        {
            var e = MakeWall(name, dims, pos);
            float bottomY = e.transform.position.y - e.transform.localScale.y * 0.5f;
            Assert.AreEqual(0f, bottomY, 0.001f, $"{name}: bottom on floor");
            Assert.AreEqual(roomH * AppConstants.MM_TO_UNITS, e.transform.localScale.y, 0.001f, $"{name}: height");
        }

        // Back and front walls span the full internal width
        Assert.AreEqual(walls[0].dims.x * AppConstants.MM_TO_UNITS, _spawned[0].transform.localScale.x, 0.001f);
        Assert.AreEqual(walls[3].dims.x * AppConstants.MM_TO_UNITS, _spawned[3].transform.localScale.x, 0.001f);

        // Left and right walls span the full internal depth
        Assert.AreEqual(walls[1].dims.z * AppConstants.MM_TO_UNITS, _spawned[1].transform.localScale.z, 0.001f);
        Assert.AreEqual(walls[2].dims.z * AppConstants.MM_TO_UNITS, _spawned[2].transform.localScale.z, 0.001f);

        // Internal dimensions check: back wall at z = +halfD, front at z = -halfD
        Assert.AreEqual(halfD, walls[0].pos.z - halfT, 0.001f, "back wall inner face");
        Assert.AreEqual(-halfD, walls[3].pos.z + halfT, 0.001f, "front wall inner face");
        Assert.AreEqual(-halfW, walls[1].pos.x + halfT, 0.001f, "left wall inner face");
        Assert.AreEqual(halfW, walls[2].pos.x - halfT, 0.001f, "right wall inner face");
    }
}
