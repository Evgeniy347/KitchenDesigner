using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
	public static class PillarMesh
	{
		private const int Segments = 24;

		public static Mesh Build(float topRadius, float midRadius, float bottomRadius,
			float topHeight, float midHeight, float bottomHeight)
		{
			float totalHeight = topHeight + midHeight + bottomHeight;
			float halfTotal = totalHeight * 0.5f;
			float topCenterY = topHeight * 0.5f + midHeight + bottomHeight - halfTotal;
			float midCenterY = midHeight * 0.5f + bottomHeight - halfTotal;
			float bottomCenterY = bottomHeight * 0.5f - halfTotal;

			var mesh = new Mesh();
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var triangles = new List<int>();

			AddCylinder(vertices, normals, uvs, triangles, topRadius, topHeight, topCenterY);
			AddCylinder(vertices, normals, uvs, triangles, midRadius, midHeight, midCenterY);
			AddCylinder(vertices, normals, uvs, triangles, bottomRadius, bottomHeight, bottomCenterY);

			mesh.vertices = vertices.ToArray();
			mesh.normals = normals.ToArray();
			mesh.uv = uvs.ToArray();
			mesh.triangles = triangles.ToArray();
			mesh.RecalculateBounds();
			return mesh;
		}

		private static void AddCylinder(List<Vector3> vertices, List<Vector3> normals,
			List<Vector2> uvs, List<int> triangles, float radius, float height, float centerY)
		{
			float halfH = height * 0.5f;
			float topY = centerY + halfH;
			float bottomY = centerY - halfH;

			int bottomCenter = vertices.Count;
			vertices.Add(new Vector3(0f, bottomY, 0f));
			normals.Add(Vector3.down);
			uvs.Add(new Vector2(0.5f, 0.5f));

			int bottomRing = vertices.Count;
			for (int i = 0; i <= Segments; i++)
			{
				float angle = i * Mathf.PI * 2f / Segments;
				float x = Mathf.Cos(angle) * radius;
				float z = Mathf.Sin(angle) * radius;
				vertices.Add(new Vector3(x, bottomY, z));
				normals.Add(Vector3.down);
				uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
			}

			for (int i = 0; i < Segments; i++)
			{
				triangles.Add(bottomCenter);
				triangles.Add(bottomRing + i);
				triangles.Add(bottomRing + i + 1);
			}

			int sideStart = vertices.Count;
			for (int i = 0; i <= Segments; i++)
			{
				float angle = i * Mathf.PI * 2f / Segments;
				float x = Mathf.Cos(angle) * radius;
				float z = Mathf.Sin(angle) * radius;
				var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				vertices.Add(new Vector3(x, bottomY, z));
				normals.Add(normal);
				uvs.Add(new Vector2(i / (float)Segments, 0f));
				vertices.Add(new Vector3(x, topY, z));
				normals.Add(normal);
				uvs.Add(new Vector2(i / (float)Segments, 1f));
			}

			for (int i = 0; i < Segments; i++)
			{
				int a = sideStart + i * 2;
				int b = sideStart + i * 2 + 1;
				int c = sideStart + (i + 1) * 2;
				int d = sideStart + (i + 1) * 2 + 1;
				triangles.Add(a);
				triangles.Add(b);
				triangles.Add(c);
				triangles.Add(c);
				triangles.Add(b);
				triangles.Add(d);
			}

			int topCenter = vertices.Count;
			vertices.Add(new Vector3(0f, topY, 0f));
			normals.Add(Vector3.up);
			uvs.Add(new Vector2(0.5f, 0.5f));

			int topRing = vertices.Count;
			for (int i = 0; i <= Segments; i++)
			{
				float angle = i * Mathf.PI * 2f / Segments;
				float x = Mathf.Cos(angle) * radius;
				float z = Mathf.Sin(angle) * radius;
				vertices.Add(new Vector3(x, topY, z));
				normals.Add(Vector3.up);
				uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
			}

			for (int i = 0; i < Segments; i++)
			{
				triangles.Add(topCenter);
				triangles.Add(topRing + i + 1);
				triangles.Add(topRing + i);
			}
		}
	}
}
