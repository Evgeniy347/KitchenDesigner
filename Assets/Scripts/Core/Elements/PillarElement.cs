using UnityEngine;

namespace KitchenDesigner.Core
{
	public class PillarElement : KitchenElement
	{
		public const int TopDiameterMM = 50;
		public const int TopHeightMM = 20;
		public const int MidDiameterMM = 20;
		public const int MidHeightMM_Min = 50;
		public const int MidHeightMM_Max = 100;
		public const int MidHeightMM_Default = 75;
		public const int BottomDiameterMM = 50;
		public const int BottomHeightMM = 10;

		[SerializeField] private int _midHeightMM = MidHeightMM_Default;

		public int MidHeightMM
		{
			get => _midHeightMM;
			set { _midHeightMM = Mathf.Clamp(value, MidHeightMM_Min, MidHeightMM_Max); ApplyDimensions(); }
		}

		public int TotalHeightMM => TopHeightMM + _midHeightMM + BottomHeightMM;

		public override void ApplyDimensions()
		{
			transform.localScale = Vector3.one;

			float toU = AppConstants.MM_TO_UNITS;
			float topR = TopDiameterMM * 0.5f * toU;
			float midR = MidDiameterMM * 0.5f * toU;
			float bottomR = BottomDiameterMM * 0.5f * toU;
			float topH = TopHeightMM * toU;
			float midH = _midHeightMM * toU;
			float bottomH = BottomHeightMM * toU;

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

			var mesh = PillarMesh.Build(topR, midR, bottomR, topH, midH, bottomH);
			meshFilter.sharedMesh = mesh;

			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

			UpdateCollider(mesh);
		}

		private void UpdateCollider(Mesh mesh)
		{
			var existing = GetComponent<Collider>();
			if (existing != null && !(existing is MeshCollider))
				Object.DestroyImmediate(existing);

			var meshCollider = GetComponent<MeshCollider>();
			if (meshCollider == null)
			{
				meshCollider = gameObject.AddComponent<MeshCollider>();
				meshCollider.convex = false;
			}
			meshCollider.sharedMesh = mesh;
		}

		public override Vector3[] GetVertices()
		{
			float toU = AppConstants.MM_TO_UNITS;
			float halfSize = TopDiameterMM * 0.5f * toU;
			float halfH = TotalHeightMM * 0.5f * toU;
			var pos = transform.position;
			var rot = transform.rotation;

			var localCorners = new Vector3[]
			{
				new Vector3(-halfSize, -halfH, -halfSize),
				new Vector3( halfSize, -halfH, -halfSize),
				new Vector3( halfSize, -halfH,  halfSize),
				new Vector3(-halfSize, -halfH,  halfSize),
				new Vector3(-halfSize,  halfH, -halfSize),
				new Vector3( halfSize,  halfH, -halfSize),
				new Vector3( halfSize,  halfH,  halfSize),
				new Vector3(-halfSize,  halfH,  halfSize),
			};

			var result = new Vector3[8];
			for (int i = 0; i < 8; i++)
				result[i] = pos + rot * localCorners[i];
			return result;
		}
	}
}
