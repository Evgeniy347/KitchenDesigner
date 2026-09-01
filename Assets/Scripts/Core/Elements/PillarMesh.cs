using UnityEngine;

namespace KitchenDesigner.Core
{
	public static class PillarMesh
	{
		public static Mesh Build(float topRadius, float midRadius, float bottomRadius,
			float topHeight, float midHeight, float bottomHeight) =>
			CylinderStackMesh.Build(
				new CylinderSection(bottomRadius, bottomHeight),
				new CylinderSection(midRadius, midHeight),
				new CylinderSection(topRadius, topHeight));
	}
}
