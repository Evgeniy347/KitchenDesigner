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

		[Undoable]
		public int MidHeightMM
		{
			get => _midHeightMM;
			set
			{
				var clamped = Mathf.Clamp(value, MidHeightMM_Min, MidHeightMM_Max);
				if (clamped == _midHeightMM) return;
				_midHeightMM = clamped;
				Data.DimensionsMM = new Vector3Int(TopDiameterMM, TotalHeightMM, TopDiameterMM);
				ApplyDimensions();
			}
		}

		public int TotalHeightMM => TopHeightMM + _midHeightMM + BottomHeightMM;

		public override void ApplyDimensions()
		{
			int desiredTotalH = Data.DimensionsMM.y;
			_midHeightMM = Mathf.Clamp(desiredTotalH - TopHeightMM - BottomHeightMM,
				MidHeightMM_Min, MidHeightMM_Max);

			transform.localScale = Vector3.one;
			Data.DimensionsMM = new Vector3Int(TopDiameterMM, TotalHeightMM, TopDiameterMM);

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

		public override Vector3[] GetVertices() => GetVerticesAt(transform.position);

		public override Vector3[] GetVerticesAt(Vector3 position)
		{
			float toU = AppConstants.MM_TO_UNITS;
			float halfSize = TopDiameterMM * 0.5f * toU;
			float halfH = TotalHeightMM * 0.5f * toU;
			var pos = position;
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

		public override Face[] GetFaces() => GetFacesAt(transform.position);

		public override Face[] GetFacesAt(Vector3 position)
		{
			float toU = AppConstants.MM_TO_UNITS;
			float w = TopDiameterMM * toU;
			float h = TotalHeightMM * toU;
			float hw = w * 0.5f;
			float hh = h * 0.5f;
			var pos = position;
			var rot = transform.rotation;

			var axes = new Vector3[]
			{
				rot * Vector3.right,
				rot * Vector3.up,
				rot * Vector3.forward
			};

			// Порядок граней — контракт базового GetFaces: +X,-X,+Y,-Y,+Z,-Z.
			// Индекс грани делится на 2 и становится осью (ResizeHandleManager,
			// ResizeMath, тесты). Раньше опора отдавала грани в порядке Y,X,Z —
			// и ручка верхней грани опоры растягивала её по X вместо высоты.
			return new Face[]
			{
				new Face(pos + axes[0] * hw,  axes[0], new Vector2(h, w), axes[1], axes[2]),
				new Face(pos - axes[0] * hw, -axes[0], new Vector2(h, w), axes[1], axes[2]),
				new Face(pos + axes[1] * hh,  axes[1], new Vector2(w, w), axes[0], axes[2]),
				new Face(pos - axes[1] * hh, -axes[1], new Vector2(w, w), axes[0], axes[2]),
				new Face(pos + axes[2] * hw,  axes[2], new Vector2(w, h), axes[0], axes[1]),
				new Face(pos - axes[2] * hw, -axes[2], new Vector2(w, h), axes[0], axes[1]),
			};
		}
	}
}
