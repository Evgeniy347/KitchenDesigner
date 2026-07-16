using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PillarElementTests
{
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
}
