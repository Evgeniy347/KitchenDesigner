using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class AssembledFacadeValidationReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = rot;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private AssembledFacadeElement MakeAssembledFacade(string name, Vector3Int dims,
        Vector3 pos, Quaternion rot, int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = rot;
        var f = go.AddComponent<AssembledFacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    [Test]
    public void GetVertices_ReturnsIdenticalResults_WhenCalledRepeatedly()
    {
        var bottom = MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        var door = MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        Vector3[] firstBottomVerts = null!;
        Vector3[] firstDoorVerts = null!;
        bool first = true;

        for (int i = 0; i < 10; i++)
        {
            var bottomVerts = bottom.GetVertices();
            var doorVerts = door.GetVertices();

            Assert.AreEqual(8, bottomVerts.Length);
            Assert.AreEqual(8, doorVerts.Length);

            if (first)
            {
                firstBottomVerts = bottomVerts;
                firstDoorVerts = doorVerts;
                first = false;
            }
            else
            {
                for (int j = 0; j < 8; j++)
                {
                    Assert.AreEqual(firstBottomVerts[j].x, bottomVerts[j].x, 1e-6f,
                        $"B3_fake_bottom vert[{j}].x differs on call {i}");
                    Assert.AreEqual(firstBottomVerts[j].y, bottomVerts[j].y, 1e-6f,
                        $"B3_fake_bottom vert[{j}].y differs on call {i}");
                    Assert.AreEqual(firstBottomVerts[j].z, bottomVerts[j].z, 1e-6f,
                        $"B3_fake_bottom vert[{j}].z differs on call {i}");

                    Assert.AreEqual(firstDoorVerts[j].x, doorVerts[j].x, 1e-6f,
                        $"B3_door vert[{j}].x differs on call {i}");
                    Assert.AreEqual(firstDoorVerts[j].y, doorVerts[j].y, 1e-6f,
                        $"B3_door vert[{j}].y differs on call {i}");
                    Assert.AreEqual(firstDoorVerts[j].z, doorVerts[j].z, 1e-6f,
                        $"B3_door vert[{j}].z differs on call {i}");
                }
            }
        }
    }

    [Test]
    public void ComputedAABB_FromVertices_IsDeterministic()
    {
        var bottom = MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        var door = MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        float firstMinX = float.NaN, firstMinY = float.NaN, firstMinZ = float.NaN;
        float firstMaxX = float.NaN, firstMaxY = float.NaN, firstMaxZ = float.NaN;

        for (int i = 0; i < 10; i++)
        {
            var aabb = ComputeAABB(door.GetVertices());

            if (float.IsNaN(firstMinX))
            {
                firstMinX = aabb.minX; firstMinY = aabb.minY; firstMinZ = aabb.minZ;
                firstMaxX = aabb.maxX; firstMaxY = aabb.maxY; firstMaxZ = aabb.maxZ;
            }
            else
            {
                Assert.AreEqual(firstMinX, aabb.minX, 1e-6f, $"minX differs on call {i}");
                Assert.AreEqual(firstMinY, aabb.minY, 1e-6f, $"minY differs on call {i}");
                Assert.AreEqual(firstMinZ, aabb.minZ, 1e-6f, $"minZ differs on call {i}");
                Assert.AreEqual(firstMaxX, aabb.maxX, 1e-6f, $"maxX differs on call {i}");
                Assert.AreEqual(firstMaxY, aabb.maxY, 1e-6f, $"maxY differs on call {i}");
                Assert.AreEqual(firstMaxZ, aabb.maxZ, 1e-6f, $"maxZ differs on call {i}");
            }
        }
    }

    [Test]
    public void Validate_WithTouchingAssembledFacade_IsDeterministic()
    {
        MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        var all = PartRegistry.GetAll();

        bool? firstHasViolation = null;
        int firstContactCount = -1;
        int firstViolationCount = -1;

        for (int i = 0; i < 10; i++)
        {
            var result = ConstraintValidator.Validate(all);

            var door = all.Find(e => e.PartName == "B3_door");
            Assert.NotNull(door);
            bool hasViolation = result.violations.Contains(door);

            if (firstHasViolation == null)
            {
                firstHasViolation = hasViolation;
                firstContactCount = result.contacts.Count;
                firstViolationCount = result.violations.Count;
            }
            else
            {
                Assert.AreEqual(firstHasViolation, hasViolation,
                    $"hasViolation for B3_door changed on call {i}: "
                    + $"expected {firstHasViolation}, got {hasViolation}");
                Assert.AreEqual(firstContactCount, result.contacts.Count,
                    $"contacts.Count changed on call {i}: "
                    + $"expected {firstContactCount}, got {result.contacts.Count}");
                Assert.AreEqual(firstViolationCount, result.violations.Count,
                    $"violations.Count changed on call {i}: "
                    + $"expected {firstViolationCount}, got {result.violations.Count}");
            }
        }
    }

    [Test]
    public void Validate_WithBothIsolated_ConsistentlyReportsViolations()
    {
        // B3_door + B3_fake_bottom без пола/стен — оба изолированы
        // AABB касаются по Z=-3.080.
        MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        var all = PartRegistry.GetAll();

        int? firstViolationCount = null;
        int? firstContactCount = null;
        bool? firstDoorInViolations = null;
        bool? firstBottomInViolations = null;

        for (int i = 0; i < 10; i++)
        {
            var result = ConstraintValidator.Validate(all);
            var doorInViolations = result.violations.Exists(v => v.PartName == "B3_door");
            var bottomInViolations = result.violations.Exists(v => v.PartName == "B3_fake_bottom");

            if (firstViolationCount == null)
            {
                firstViolationCount = result.violations.Count;
                firstContactCount = result.contacts.Count;
                firstDoorInViolations = doorInViolations;
                firstBottomInViolations = bottomInViolations;
            }
            else
            {
                Assert.AreEqual(firstViolationCount, result.violations.Count,
                    $"violations.Count changed on iteration {i}: "
                    + $"expected {firstViolationCount}, got {result.violations.Count}");
                Assert.AreEqual(firstContactCount, result.contacts.Count,
                    $"contacts.Count changed on iteration {i}: "
                    + $"expected {firstContactCount}, got {result.contacts.Count}");
                Assert.AreEqual(firstDoorInViolations, doorInViolations,
                    $"B3_door violation state changed on iteration {i}: "
                    + $"expected {firstDoorInViolations}, got {doorInViolations}");
                Assert.AreEqual(firstBottomInViolations, bottomInViolations,
                    $"B3_fake_bottom violation state changed on iteration {i}: "
                    + $"expected {firstBottomInViolations}, got {bottomInViolations}");
            }
        }
    }

    [Test]
    public void Validate_WithFullScene_ConsistentViolationState()
    {
        var plate = MakeElement("BasePlate",
            new Vector3Int(3170, 18, 7240),
            new Vector3(0f, -0.009f, 0f),
            Quaternion.identity);
        plate.gameObject.AddComponent<BasePlate>();

        MakeElement("BackWall",
            new Vector3Int(3170, 2700, 100),
            new Vector3(0f, 1.35f, 3.67f),
            Quaternion.identity).gameObject.AddComponent<Wall>();

        MakeElement("LeftWall",
            new Vector3Int(100, 2700, 7240),
            new Vector3(-1.635f, 1.35f, 0f),
            Quaternion.identity).gameObject.AddComponent<Wall>();

        MakeElement("A",
            new Vector3Int(100, 2700, 7240),
            new Vector3(1.635f, 1.35f, 0f),
            Quaternion.identity).gameObject.AddComponent<Wall>();

        MakeElement("B",
            new Vector3Int(3170, 2700, 100),
            new Vector3(0f, 1.35f, -3.67f),
            Quaternion.identity).gameObject.AddComponent<Wall>();

        MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        var all = PartRegistry.GetAll();

        bool? firstHasViolation = null;
        int firstCount = -1;

        for (int i = 0; i < 10; i++)
        {
            var result = ConstraintValidator.Validate(all);

            var door = all.Find(e => e.PartName == "B3_door");
            bool hasViolation = result.violations.Contains(door);

            if (firstHasViolation == null)
            {
                firstHasViolation = hasViolation;
                firstCount = result.violations.Count;
            }
            else
            {
                Assert.AreEqual(firstHasViolation, hasViolation,
                    $"B3_door violation state changed on iteration {i}: "
                    + $"was {firstHasViolation}, now {hasViolation}");
                Assert.AreEqual(firstCount, result.violations.Count,
                    $"violations.Count changed on iteration {i}: "
                    + $"was {firstCount}, now {result.violations.Count}");
            }
        }
    }

    [Test]
    public void LoweredWall_GetVertices_ReturnsFullGeometry()
    {
        var wall = MakeElement("TestWall",
            new Vector3Int(3170, 2700, 100),
            new Vector3(0f, 1.35f, -3.67f),
            Quaternion.identity);
        var wallComp = wall.gameObject.AddComponent<Wall>();

        var fullVerts = wall.GetVertices();
        wallComp.SetLowered(true, 0.1f);
        var loweredVerts = wall.GetVertices();

        for (int i = 0; i < 8; i++)
        {
            Assert.AreEqual(fullVerts[i].x, loweredVerts[i].x, 1e-6f,
                $"vert[{i}].x should be full even when lowered");
            Assert.AreEqual(fullVerts[i].y, loweredVerts[i].y, 1e-6f,
                $"vert[{i}].y should be full even when lowered");
            Assert.AreEqual(fullVerts[i].z, loweredVerts[i].z, 1e-6f,
                $"vert[{i}].z should be full even when lowered");
        }
    }

    [Test]
    public void LoweredWall_GetFaces_ReturnsFullGeometry()
    {
        var wall = MakeElement("TestWall",
            new Vector3Int(3170, 2700, 100),
            new Vector3(0f, 1.35f, -3.67f),
            Quaternion.identity);
        var wallComp = wall.gameObject.AddComponent<Wall>();

        var fullFaces = wall.GetFaces();
        wallComp.SetLowered(true, 0.1f);
        var loweredFaces = wall.GetFaces();

        Assert.AreEqual(fullFaces[4].size.y, loweredFaces[4].size.y, 1e-6f,
            "Z+ face height should be full even when wall is lowered");
        Assert.AreEqual(fullFaces[4].center.y, loweredFaces[4].center.y, 1e-6f,
            "Z+ face center.y should be full even when wall is lowered");
    }

    [Test]
    public void LoweredWall_Validation_FindsContactWithB3FakeBottom()
    {
        var wall = MakeElement("TestWall",
            new Vector3Int(3170, 2700, 100),
            new Vector3(0f, 1.35f, -3.67f),
            Quaternion.identity);
        var wallComp = wall.gameObject.AddComponent<Wall>();

        var bottom = MakeElement("B3_fake_bottom",
            new Vector3Int(564, 540, 16),
            new Vector3(0.127f, 0.108f, -3.35f),
            Quaternion.Euler(270f, 0f, 0f));

        var door = MakeAssembledFacade("B3_door",
            new Vector3Int(596, 716, 18),
            new Vector3(0.127f, 0.46f, -3.071f),
            Quaternion.identity,
            0, 0, 0, 0);

        var all = PartRegistry.GetAll();

        // Без опускания: B3_fake_bottom контактирует со стеной → оба валидны
        var resultFull = ConstraintValidator.Validate(all);
        var doorFull = all.Find(e => e.PartName == "B3_door");
        var bottomFull = all.Find(e => e.PartName == "B3_fake_bottom");
        Assert.IsFalse(resultFull.violations.Contains(doorFull),
            "B3_door should be valid when wall is full height");
        Assert.IsFalse(resultFull.violations.Contains(bottomFull),
            "B3_fake_bottom should be valid when wall is full height");

        // Опускаем стену — валидация всё равно должна видеть полную геометрию
        wallComp.SetLowered(true, 0.1f);
        var resultLowered = ConstraintValidator.Validate(all);
        Assert.IsFalse(resultLowered.violations.Contains(doorFull),
            "B3_door should remain valid even when wall is lowered");
        Assert.IsFalse(resultLowered.violations.Contains(bottomFull),
            "B3_fake_bottom should remain valid even when wall is lowered");

        // Количество контактов и нарушений должно совпадать
        Assert.AreEqual(resultFull.contacts.Count, resultLowered.contacts.Count,
            "contact count should not change when wall is lowered");
        Assert.AreEqual(resultFull.violations.Count, resultLowered.violations.Count,
            "violation count should not change when wall is lowered");
    }

    private static (float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        ComputeAABB(Vector3[] v)
    {
        float minX = v[0].x, maxX = v[0].x;
        float minY = v[0].y, maxY = v[0].y;
        float minZ = v[0].z, maxZ = v[0].z;
        for (int i = 1; i < v.Length; i++)
        {
            if (v[i].x < minX) minX = v[i].x; else if (v[i].x > maxX) maxX = v[i].x;
            if (v[i].y < minY) minY = v[i].y; else if (v[i].y > maxY) maxY = v[i].y;
            if (v[i].z < minZ) minZ = v[i].z; else if (v[i].z > maxZ) maxZ = v[i].z;
        }
        return (minX, minY, minZ, maxX, maxY, maxZ);
    }
}

