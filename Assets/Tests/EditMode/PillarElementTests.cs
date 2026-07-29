using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PillarElementTests
{
	private readonly List<GameObject> _spawned = new List<GameObject>();

	[TearDown]
	public void Teardown()
	{
		foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
		_spawned.Clear();
		CommandStack.Clear();
	}

	private static Vector3 AabbExtent(KitchenElement e)
	{
		var v = e.GetVertices();
		Vector3 min = v[0], max = v[0];
		for (int i = 1; i < v.Length; i++)
		{
			min = Vector3.Min(min, v[i]);
			max = Vector3.Max(max, v[i]);
		}
		return max - min;
	}

	private static Vector3 AabbCenter(KitchenElement e)
	{
		var v = e.GetVertices();
		Vector3 min = v[0], max = v[0];
		for (int i = 1; i < v.Length; i++)
		{
			min = Vector3.Min(min, v[i]);
			max = Vector3.Max(max, v[i]);
		}
		return (min + max) * 0.5f;
	}

	private GameObject MakeBoard(Vector3 pos, Vector3Int dims)
	{
		var go = ElementFactory.CreatePart(dims, "Board", pos);
		_spawned.Add(go);
		return go;
	}

	private GameObject MakePillar(int midHeightMM, Vector3 pos)
	{
		var go = ElementFactory.CreatePillar(midHeightMM, "TestPillar", pos);
		_spawned.Add(go);
		return go;
	}

	[Test]
	public void Pillar_BoundingBox_DefaultHeight()
	{
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "TestPillar", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		int expectedH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Default + PillarElement.BottomHeightMM;
		float toU = AppConstants.MM_TO_UNITS;

		var extent = AabbExtent(pillar);
		Assert.AreEqual(PillarElement.TopDiameterMM * toU, extent.x, 0.001f, "ширина 50мм");
		Assert.AreEqual(expectedH * toU, extent.y, 0.001f, "высота TotalHeightMM");
		Assert.AreEqual(PillarElement.TopDiameterMM * toU, extent.z, 0.001f, "глубина 50мм");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_BoundingBox_MinHeight()
	{
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Min, "TestPillarMin", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		int expectedH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Min + PillarElement.BottomHeightMM;
		float toU = AppConstants.MM_TO_UNITS;

		var extent = AabbExtent(pillar);
		Assert.AreEqual(expectedH * toU, extent.y, 0.001f, "высота при min midHeight 80мм");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_BoundingBox_MaxHeight()
	{
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Max, "TestPillarMax", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		int expectedH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Max + PillarElement.BottomHeightMM;
		float toU = AppConstants.MM_TO_UNITS;

		var extent = AabbExtent(pillar);
		Assert.AreEqual(expectedH * toU, extent.y, 0.001f, "высота при max midHeight 130мм");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_FloorSnap_BottomAtFloor()
	{
		int totalH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Default + PillarElement.BottomHeightMM;
		float posY = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "TestPillarSnap", new Vector3(0, posY, 0));
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		var extent = AabbExtent(pillar);
		var center = AabbCenter(pillar);
		var minY = center.y - extent.y * 0.5f;
		Assert.AreEqual(0f, minY, 0.001f, "низ опоры на полу (y=0)");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_MidHeight_ClampMin()
	{
		var go = ElementFactory.CreatePillar(10, "TestPillarClampMin", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);
		Assert.AreEqual(PillarElement.MidHeightMM_Min, pillar.MidHeightMM, "clamp к минимуму 50");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_MidHeight_ClampMax()
	{
		var go = ElementFactory.CreatePillar(200, "TestPillarClampMax", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);
		Assert.AreEqual(PillarElement.MidHeightMM_Max, pillar.MidHeightMM, "clamp к максимуму 100");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_GetVertices_Returns8Corners()
	{
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "TestPillarVerts", Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		var verts = pillar.GetVertices();
		Assert.AreEqual(8, verts.Length, "8 углов AABB");

		Object.DestroyImmediate(go);
	}

	[Test]
	public void Pillar_DimensionsMM_SyncedAfterMidHeightChange()
	{
		int totalH = PillarElement.TopHeightMM + 80 + PillarElement.BottomHeightMM;
		float posY = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = MakePillar(PillarElement.MidHeightMM_Default, new Vector3(0, posY, 0));
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		Assert.AreEqual(105, pillar.DimensionsMM.y, "init height = 20+75+10");
		pillar.MidHeightMM = 80;
		Assert.AreEqual(80, pillar.MidHeightMM, "mid height changed");
		Assert.AreEqual(110, pillar.DimensionsMM.y, "DimensionsMM synced: 20+80+10");

		var extent = AabbExtent(pillar);
		Assert.AreEqual(110f * AppConstants.MM_TO_UNITS, extent.y, 0.001f, "AABB height matches");
	}

	[Test]
	public void Pillar_DimensionsMM_SyncedAfterPropertySett()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		pillar.DimensionsMM = new Vector3Int(50, 130, 50);
		Assert.AreEqual(100, pillar.MidHeightMM, "mid = 130-20-10=100");
		Assert.AreEqual(130, pillar.DimensionsMM.y, "DimensionsMM synced");

		pillar.DimensionsMM = new Vector3Int(50, 70, 50);
		Assert.AreEqual(PillarElement.MidHeightMM_Min, pillar.MidHeightMM, "mid = clamp(70-30, 50, 100) = 50");
		Assert.AreEqual(80, pillar.DimensionsMM.y, "DimensionsMM = 20+50+10");
	}

	[Test]
	public void Pillar_UndoRestoresMidHeight()
	{
		int totalH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Default + PillarElement.BottomHeightMM;
		float posY = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = MakePillar(PillarElement.MidHeightMM_Default, new Vector3(0, posY, 0));
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		var dimsBefore = pillar.DimensionsMM;
		Assert.AreEqual(105, dimsBefore.y);

		pillar.DimensionsMM = new Vector3Int(50, 130, 50);
		Assert.AreEqual(130, pillar.DimensionsMM.y);
		Assert.AreEqual(100, pillar.MidHeightMM);

		pillar.DimensionsMM = dimsBefore;
		Assert.AreEqual(105, pillar.DimensionsMM.y, "DimensionsMM restored");
		Assert.AreEqual(75, pillar.MidHeightMM, "mid height restored");
	}

	[Test]
	public void Pillar_ResizeCommand_UndoRedo_Works()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		var oldDims = pillar.DimensionsMM;
		var oldPos = pillar.transform.position;
		var newDims = new Vector3Int(50, 130, 50);
		var newPos = new Vector3(1, 0, 0);

		var cmd = new ResizeCommand(pillar, oldDims, newDims, oldPos, newPos,
			Quaternion.identity, Quaternion.identity);
		cmd.Execute();

		Assert.AreEqual(130, pillar.DimensionsMM.y, "dims after execute");
		Assert.AreEqual(100, pillar.MidHeightMM, "mid height after execute");
		Assert.AreEqual(newPos, pillar.transform.position, "pos after execute");

		cmd.Undo();
		Assert.AreEqual(oldDims.y, pillar.DimensionsMM.y, "dims after undo");
		Assert.AreEqual(PillarElement.MidHeightMM_Default, pillar.MidHeightMM, "mid after undo");
		Assert.AreEqual(oldPos, pillar.transform.position, "pos after undo");

		cmd.Execute();
		Assert.AreEqual(130, pillar.DimensionsMM.y, "dims after redo");
		Assert.AreEqual(100, pillar.MidHeightMM, "mid after redo");
	}

	[Test]
	public void Pillar_MoveWithAutoAdjust_UndoRestoresBothPositionAndHeight()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
		var board = MakeBoard(new Vector3(0, 0.108f, 0), new Vector3Int(540, 16, 564));

		int midH = 75;
		int totalH = PillarElement.TopHeightMM + midH + PillarElement.BottomHeightMM;
		float pillarY = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = MakePillar(midH, new Vector3(0, pillarY, 0));
		var pillar = go.GetComponent<PillarElement>();
		Assert.AreEqual(105, pillar.TotalHeightMM);

		var bottomY = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.AreEqual(0f, bottomY, 0.001f, "pillar bottom at floor");

		var verts = pillar.GetVertices();
		float topY = float.MinValue;
		foreach (var v in verts) if (v.y > topY) topY = v.y;
		Assert.GreaterOrEqual(topY, board.transform.position.y - 0.008f, "pillar top reaches board bottom");

		pillar.transform.position = new Vector3(0, pillar.transform.position.y - 0.01f, 0);
		Assert.AreEqual(105, pillar.TotalHeightMM, "height unchanged before auto-adjust");

		var topBefore = float.MinValue;
		foreach (var v in pillar.GetVertices()) if (v.y > topBefore) topBefore = v.y;
		Assert.Less(topBefore, board.transform.position.y - 0.008f, "gap appears after lowering");
	}

	[Test]
	public void Pillar_SaveFileScenario_FloorAndPillar_NoViolations()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3170, 18, 7240));

		var pillarGo = MakePillar(75, new Vector3(1.139f, 0.052f, -2.503f));
		var pillar = pillarGo.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);
		Assert.AreEqual(105, pillar.TotalHeightMM, "height from save: 20+75+10=105");
		Assert.AreEqual(105, pillar.DimensionsMM.y, "DimensionsMM synced to total height");

		var bottomY = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.LessOrEqual(bottomY, 0.001f, "pillar bottom near/at floor level");

		pillar.transform.position = new Vector3(1.139f, 0.052f - 0.02f, -2.503f);
		Assert.AreEqual(105, pillar.TotalHeightMM, "height unchanged by drag alone");
		Assert.AreEqual(105, pillar.DimensionsMM.y, "DimensionsMM intact");

		pillar.DimensionsMM = new Vector3Int(50, 130, 50);
		Assert.AreEqual(100, pillar.MidHeightMM, "mid height derived from DimensionsMM");
		Assert.AreEqual(130, pillar.DimensionsMM.y, "DimensionsMM synced");

		pillar.MidHeightMM = 60;
		Assert.AreEqual(90, pillar.DimensionsMM.y, "DimensionsMM follows MidHeightMM change");
		Assert.AreEqual(60, pillar.MidHeightMM);

		pillar.DimensionsMM = new Vector3Int(50, 50, 50);
		Assert.AreEqual(PillarElement.MidHeightMM_Min, pillar.MidHeightMM, "midHeight clamped to min");
		Assert.AreEqual(80, pillar.DimensionsMM.y, "DimensionsMM = 20+50+10");
	}

	[Test]
	public void Pillar_DimensionsMM_BidiConsistency_RoundTrip()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();

		for (int h = 80; h <= 130; h += 5)
		{
			pillar.DimensionsMM = new Vector3Int(50, h, 50);
			int expectedMid = Mathf.Clamp(h - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
				PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
			int expectedH = PillarElement.TopHeightMM + expectedMid + PillarElement.BottomHeightMM;

			Assert.AreEqual(expectedMid, pillar.MidHeightMM, $"mid height for input {h}");
			Assert.AreEqual(expectedH, pillar.DimensionsMM.y, $"DimensionsMM synced for input {h}");

			pillar.MidHeightMM = expectedMid;
			Assert.AreEqual(expectedH, pillar.DimensionsMM.y, $"DimensionsMM stays after re-set mid {expectedMid}");
		}
	}

	[Test]
	public void Pillar_CompositeCommand_MoveAndResize_UndoRestoresBoth()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.AreEqual(105, pillar.DimensionsMM.y);
		Assert.AreEqual(75, pillar.MidHeightMM);

		var posBefore = pillar.transform.position;
		var dimsBefore = pillar.DimensionsMM;
		var dimsAfter = new Vector3Int(50, 130, 50);

		var cmd = new CompositeCommand("move+resize", new List<IUndoCommand>
		{
			new MoveCommand(pillar, posBefore, posBefore, Quaternion.identity, Quaternion.identity),
			new ResizeCommand(pillar, dimsBefore, dimsAfter,
				posBefore, posBefore, Quaternion.identity, Quaternion.identity),
		});

		cmd.Execute();
		Assert.AreEqual(posBefore, pillar.transform.position, "position unchanged by resize+move");
		Assert.AreEqual(130, pillar.DimensionsMM.y, "dims after execute");
		Assert.AreEqual(100, pillar.MidHeightMM, "mid height after execute");

		cmd.Undo();
		Assert.AreEqual(posBefore, pillar.transform.position, "position after undo");
		Assert.AreEqual(105, pillar.DimensionsMM.y, "dims after undo");
		Assert.AreEqual(75, pillar.MidHeightMM, "mid height after undo");

		cmd.Execute();
		Assert.AreEqual(130, pillar.DimensionsMM.y, "dims after redo");
		Assert.AreEqual(100, pillar.MidHeightMM, "mid after redo");
	}

	[Test]
	public void Pillar_CompositeCommand_UndoReverseOrder_CorrectState()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.AreEqual(75, pillar.MidHeightMM);

		var origPos = pillar.transform.position;
		var origDims = pillar.DimensionsMM;
		var dragPos = new Vector3(2, 0, 0);
		var adjPos = new Vector3(2, 0.01f, 0);
		var adjDims = new Vector3Int(50, 130, 50);

		var cmd = new CompositeCommand("drag+adjust", new List<IUndoCommand>
		{
			new MoveCommand(pillar, origPos, adjPos, Quaternion.identity, Quaternion.identity),
			new ResizeCommand(pillar, origDims, adjDims,
				dragPos, adjPos, Quaternion.identity, Quaternion.identity),
		});

		cmd.Execute();
		Assert.AreEqual(adjPos, pillar.transform.position);
		Assert.AreEqual(130, pillar.DimensionsMM.y);
		Assert.AreEqual(100, pillar.MidHeightMM);

		cmd.Undo();
		Assert.AreEqual(origPos, pillar.transform.position, "pos back to original");
		Assert.AreEqual(105, pillar.DimensionsMM.y, "dims back to original");
		Assert.AreEqual(75, pillar.MidHeightMM, "mid back to original");
	}

	[Test]
	public void Pillar_CreatePillar_DimensionsMM_SyncedFromStart()
	{
		var go = ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "TestCreate", Vector3.zero);
		_spawned.Add(go);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		int expectedH = PillarElement.TopHeightMM + PillarElement.MidHeightMM_Default + PillarElement.BottomHeightMM;
		Assert.AreEqual(expectedH, pillar.DimensionsMM.y, "DimensionsMM synced after factory creation");
		Assert.AreEqual(PillarElement.MidHeightMM_Default, pillar.MidHeightMM, "mid height as requested");
		Assert.AreEqual(expectedH, pillar.TotalHeightMM, "total height matches");
	}

	[Test]
	public void Pillar_CreatePillar_CustomMidHeight_DimensionsMM_Synced()
	{
		var go = ElementFactory.CreatePillar(60, "TestCreate60", Vector3.zero);
		_spawned.Add(go);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		int expectedH = PillarElement.TopHeightMM + 60 + PillarElement.BottomHeightMM;
		Assert.AreEqual(60, pillar.MidHeightMM);
		Assert.AreEqual(expectedH, pillar.DimensionsMM.y, "DimensionsMM synced for mid=60");
		Assert.AreEqual(expectedH, pillar.TotalHeightMM);
	}

	[Test]
	public void Pillar_MidHeight_SetSameValue_NoSpuriousChange()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.AreEqual(75, pillar.MidHeightMM);

		int totalBefore = pillar.DimensionsMM.y;
		pillar.MidHeightMM = PillarElement.MidHeightMM_Default;
		Assert.AreEqual(75, pillar.MidHeightMM, "mid unchanged");
		Assert.AreEqual(totalBefore, pillar.DimensionsMM.y, "DimensionsMM unchanged");
	}

	[Test]
	public void Pillar_SnapsToFloor_WhenLowered()
	{
		var s = KitchenSettings.Instance;
		Assert.IsNotNull(s);
		s.SnapEnabled = true;
		s.SnapThreshold = 50f;
		s.GridEnabled = false;

		var floorGo = new GameObject("Floor");
		floorGo.transform.position = Vector3.zero;
		var floor = floorGo.AddComponent<KitchenElement>();
		floor.DimensionsMM = new Vector3Int(3000, 18, 3000);
		_spawned.Add(floorGo);

		int totalH = PillarElement.TopHeightMM + 75 + PillarElement.BottomHeightMM;
		float halfH = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = MakePillar(75, new Vector3(0, halfH + 0.009f, 0));
		var pillar = go.GetComponent<PillarElement>();

		var snapped = SnapSystem.TrySnap(pillar,
			new List<KitchenElement> { floor },
			new Vector3(0, halfH + 0.009f, 0));
		Assert.IsTrue(snapped.snapped, "pillar should snap to floor at correct height");

		pillar.transform.position = new Vector3(0, halfH + 0.04f, 0);
		var fromAbove = SnapSystem.TrySnap(pillar,
			new List<KitchenElement> { floor },
			new Vector3(0, halfH + 0.04f, 0));
		Assert.IsTrue(fromAbove.snapped, "pillar 4cm above floor should snap");
		Assert.AreEqual(halfH + 0.009f, fromAbove.position.y, Tol, "snapped Y when lowered");

		pillar.transform.position = new Vector3(0, halfH + 0.10f, 0);
		var fromFar = SnapSystem.TrySnap(pillar,
			new List<KitchenElement> { floor },
			new Vector3(0, halfH + 0.10f, 0));
		Assert.IsFalse(fromFar.snapped, "pillar too far above floor should NOT snap");

		pillar.transform.position = fromAbove.position;
		Assert.IsFalse(SnapSystem.ElementsIntersect(pillar, floor),
			"pillar and floor should not intersect after snap");
	}

	[Test]
	public void Pillar_NotOnFloor_FloorFoundByAABB()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
		var boardGo = MakeBoard(new Vector3(0, 0.108f, 0), new Vector3Int(540, 16, 564));
		var board = boardGo.GetComponent<KitchenElement>();

		var go = MakePillar(75, new Vector3(0, 0.08f, 0));
		var pillar = go.GetComponent<PillarElement>();

		float bottomBefore = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.Greater(bottomBefore, 0.02f,
			"pillar bottom >20mm above floor — but AABB should still find floor");

		var boardBottom = float.MaxValue;
		foreach (var v in board.GetVertices()) if (v.y < boardBottom) boardBottom = v.y;
		float floorTop = floor.transform.position.y + 9f * AppConstants.MM_TO_UNITS;
		Assert.Greater(floorTop, -0.5f, "floor top exists");

		float gapM = boardBottom - floorTop;
		int gapMM = Mathf.RoundToInt(gapM / AppConstants.MM_TO_UNITS);
		int neededMid = Mathf.Clamp(
			gapMM - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
			PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);

		Assert.GreaterOrEqual(neededMid, PillarElement.MidHeightMM_Min,
			"gap should be fillable by pillar (mid height in range)");
		Assert.LessOrEqual(neededMid, PillarElement.MidHeightMM_Max,
			"mid height within max range");
	}

	[Test]
	public void Pillar_AutoAdjust_WhenDraggedUnderBoard_FillsGap()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
		var boardGo = MakeBoard(new Vector3(0, 0.108f, 0), new Vector3Int(540, 16, 564));
		var board = boardGo.GetComponent<KitchenElement>();

		var go = MakePillar(75, new Vector3(0, 0.08f, 0));
		var pillar = go.GetComponent<PillarElement>();

		var boardBottom = float.MaxValue;
		foreach (var v in board.GetVertices()) if (v.y < boardBottom) boardBottom = v.y;
		float floorTop = floor.transform.position.y + 9f * AppConstants.MM_TO_UNITS;

		int neededMid = Mathf.RoundToInt((boardBottom - floorTop) / AppConstants.MM_TO_UNITS)
			- PillarElement.TopHeightMM - PillarElement.BottomHeightMM;
		neededMid = Mathf.Clamp(neededMid, PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
		pillar.MidHeightMM = neededMid;

		pillar.transform.position = new Vector3(0, floorTop + pillar.TotalHeightMM * 0.5f * AppConstants.MM_TO_UNITS, 0);

		var topAfter = float.MinValue;
		foreach (var v in pillar.GetVertices()) if (v.y > topAfter) topAfter = v.y;
		Assert.Less(Mathf.Abs(topAfter - boardBottom), 0.001f,
			"pillar top reaches board bottom after manual adjust");

		var bottomAfter = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.Less(Mathf.Abs(bottomAfter - floorTop), 0.001f,
			"pillar bottom at floor after manual adjust");
	}

	[Test]
	public void Pillar_SaveFilePos_FarFromFloorCenter_FloorFoundByAABB()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3170, 18, 7240));
		var boardGo = MakeBoard(new Vector3(1.315f, 0.108f, -2.162f), new Vector3Int(540, 16, 564));
		var board = boardGo.GetComponent<KitchenElement>();

		var go = MakePillar(75, new Vector3(1.139f, 0.052f, -2.503f));
		var pillar = go.GetComponent<PillarElement>();

		Assert.AreEqual(105, pillar.TotalHeightMM);
		Assert.AreEqual(105, pillar.DimensionsMM.y);

		float pillarCx = pillar.transform.position.x;
		float pillarCz = pillar.transform.position.z;

		float floorMinX = floor.transform.position.x - 1585f * AppConstants.MM_TO_UNITS;
		float floorMaxX = floor.transform.position.x + 1585f * AppConstants.MM_TO_UNITS;
		float floorMinZ = floor.transform.position.z - 3620f * AppConstants.MM_TO_UNITS;
		float floorMaxZ = floor.transform.position.z + 3620f * AppConstants.MM_TO_UNITS;

		Assert.Greater(pillarCx, floorMinX - 0.001f, "pillar X inside floor AABB");
		Assert.Less(pillarCx, floorMaxX + 0.001f, "pillar X inside floor AABB");
		Assert.Greater(pillarCz, floorMinZ - 0.001f, "pillar Z inside floor AABB");
		Assert.Less(pillarCz, floorMaxZ + 0.001f, "pillar Z inside floor AABB");

		float floorTop = floor.transform.position.y + 9f * AppConstants.MM_TO_UNITS;
		float pillarBottom = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.Less(Mathf.Abs(pillarBottom - floorTop), 0.001f,
			"pillar sits on floor — bottom at floor top level");
	}

	[Test]
	public void Pillar_DraggedUnderBoard_OffFloor_AutoAdjustFillsGap()
	{
		var floor = MakeBoard(new Vector3(0, -0.009f, 0), new Vector3Int(3000, 18, 3000));
		var boardGo = MakeBoard(new Vector3(0, 0.108f, 0), new Vector3Int(540, 16, 564));
		var board = boardGo.GetComponent<KitchenElement>();

		var go = MakePillar(75, new Vector3(0, 0.09f, 0));
		var pillar = go.GetComponent<PillarElement>();

		float bottomBefore = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.Greater(bottomBefore, 0.02f, "pillar bottom >20mm above floor");

		var boardBottom = float.MaxValue;
		foreach (var v in board.GetVertices()) if (v.y < boardBottom) boardBottom = v.y;
		float floorTop = floor.transform.position.y + 9f * AppConstants.MM_TO_UNITS;

		pillar.transform.position = new Vector3(0,
			floorTop + pillar.TotalHeightMM * 0.5f * AppConstants.MM_TO_UNITS, 0);

		int gapMM = Mathf.RoundToInt((boardBottom - floorTop) / AppConstants.MM_TO_UNITS);
		int neededMid = Mathf.Clamp(
			gapMM - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
			PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
		Assert.GreaterOrEqual(neededMid, PillarElement.MidHeightMM_Min);
		Assert.LessOrEqual(neededMid, PillarElement.MidHeightMM_Max);

		pillar.MidHeightMM = neededMid;
		pillar.transform.position = new Vector3(0,
			floorTop + pillar.TotalHeightMM * 0.5f * AppConstants.MM_TO_UNITS, 0);

		var topAfter = float.MinValue;
		foreach (var v in pillar.GetVertices()) if (v.y > topAfter) topAfter = v.y;
		Assert.Less(Mathf.Abs(topAfter - boardBottom), 0.001f,
			"pillar top reaches board bottom after floor-snap + height-adjust");

		var bottomAfter = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.Less(Mathf.Abs(bottomAfter - floorTop), 0.001f,
			"pillar bottom at floor");
	}

	/// <summary>Порядок граней — общий контракт: индекс/2 = ось (0=X,1=Y,2=Z),
	/// чётный индекс = «плюс». На этом стоит вся ресайз-ручка (_axisIndex =
	/// faceIndex/2). Опора отдавала грани в порядке Y,X,Z — и ручка верхней
	/// грани растягивала ногу по ширине вместо высоты.</summary>
	[Test]
	public void Pillar_GetFaces_AxisOrderMatchesBaseContract()
	{
		var go = MakePillar(PillarElement.MidHeightMM_Default, Vector3.zero);
		var pillar = go.GetComponent<PillarElement>();
		Assert.IsNotNull(pillar);

		var faces = pillar.GetFaces();
		Assert.AreEqual(6, faces.Length);

		var expected = new[]
		{
			Vector3.right, Vector3.left,
			Vector3.up, Vector3.down,
			Vector3.forward, Vector3.back,
		};
		for (int i = 0; i < 6; i++)
			Assert.AreEqual(1f, Vector3.Dot(faces[i].normal.normalized, expected[i]), Tol,
				$"грань {i} должна смотреть в {expected[i]}");

		// Та же деталь, но обычная коробка — грани обязаны идти в том же порядке.
		var board = MakeBoard(Vector3.zero, new Vector3Int(50, 105, 50)).GetComponent<KitchenElement>();
		var boardFaces = board.GetFaces();
		for (int i = 0; i < 6; i++)
			Assert.AreEqual(1f, Vector3.Dot(faces[i].normal.normalized, boardFaces[i].normal.normalized), Tol,
				$"порядок грани {i} расходится с базовым KitchenElement");
	}

	/// <summary>Ресайз ноги за верхнюю грань: высота зажимается типом детали
	/// (80..130 мм), и центр обязан считаться от ПРИНЯТОГО размера — иначе низ
	/// ноги отрывается от пола на половину отброшенной дельты.</summary>
	[Test]
	public void Pillar_ResizeBeyondMaxHeight_BottomStaysOnFloor()
	{
		int midH = 75;
		int totalH = PillarElement.TopHeightMM + midH + PillarElement.BottomHeightMM; // 105
		float halfH = totalH * 0.5f * AppConstants.MM_TO_UNITS;
		var go = MakePillar(midH, new Vector3(0, halfH, 0));
		var pillar = go.GetComponent<PillarElement>();

		float bottomBefore = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.AreEqual(0f, bottomBefore, Tol, "низ ноги на полу");

		// Тянем верхнюю грань (+Y) вверх на 200 мм — деталь примет только 130 мм.
		int axis = 1;
		float sizeStart = totalH * AppConstants.MM_TO_UNITS;
		var topFace = pillar.GetFaces()[2];
		ResizeMath.Compute(pillar.DimensionsMM, axis, topFace.normal, topFace.center,
			topFace.rightAxis, topFace.upAxis, topFace.size,
			pillar.transform.position, sizeStart, 200f * AppConstants.MM_TO_UNITS,
			new List<ElementGeometry>(), pillar.ToGeometry(), snapEnabled: false, 0f,
			out var newDims, out _, out _);

		pillar.DimensionsMM = newDims;
		pillar.transform.position = ResizeMath.CenterForAppliedDims(
			new Vector3(0, halfH, 0), topFace.normal, sizeStart, pillar.DimensionsMM, axis);

		Assert.AreEqual(130, pillar.DimensionsMM.y, "высота зажата максимумом");
		float bottomAfter = AabbCenter(pillar).y - AabbExtent(pillar).y * 0.5f;
		Assert.AreEqual(0f, bottomAfter, Tol, "низ ноги остался на полу после зажатого ресайза");
	}

	private const float Tol = 0.001f;
}
