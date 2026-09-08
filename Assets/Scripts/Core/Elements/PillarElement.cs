using UnityEngine;

namespace KitchenDesigner.Core
{
	public class PillarElement : KitchenElement, IAutoSeated
	{

        public override string DisplayTypeName => "Опора";

		public void SeatAfterMove(System.Collections.Generic.IReadOnlyList<KitchenElement> scene,
			SnapCursor cursor = default) =>
			PillarAutoFit.Seat(this, scene);

		public void RepairJointAfterGridSnap(
			System.Collections.Generic.IReadOnlyList<KitchenElement> scene) =>
			PillarAutoFit.Seat(this, scene);
		public const int DiameterMM_Default = 50;
		public const int DiameterMM_Min = 20;
		public const int DiameterMM_Max = 200;
		public const float MidDiameterRatio = 0.4f;
		public const int TopHeightMM = 20;
		public const int MidHeightMM_Min = 50;
		public const int MidHeightMM_Max = 100;
		public const int MidHeightMM_Default = 75;
		public const int BottomHeightMM = 10;

		[SerializeField] private int _midHeightMM = MidHeightMM_Default;
		[SerializeField] private int _diameterMM = DiameterMM_Default;

		[Undoable]
		public int MidHeightMM
		{
			get => _midHeightMM;
			set
			{
				var clamped = Mathf.Clamp(value, MidHeightMM_Min, MidHeightMM_Max);
				if (clamped == _midHeightMM) return;
				_midHeightMM = clamped;
				Data.DimensionsMM = new Vector3Int(_diameterMM, TotalHeightMM, _diameterMM);
				ApplyDimensions();
			}
		}

		[Undoable]
		public int DiameterMM
		{
			get => _diameterMM;
			set
			{
				var clamped = Mathf.Clamp(value, DiameterMM_Min, DiameterMM_Max);
				if (clamped == _diameterMM) return;
				_diameterMM = clamped;
				Data.DimensionsMM = new Vector3Int(_diameterMM, TotalHeightMM, _diameterMM);
				ApplyDimensions();
			}
		}

		public int TotalHeightMM => TopHeightMM + _midHeightMM + BottomHeightMM;

		public int MidDiameterMM =>
			Mathf.Max(1, Mathf.RoundToInt(_diameterMM * MidDiameterRatio));

		public override void ApplyDimensions()
		{
			int desiredTotalH = Data.DimensionsMM.y;
			_midHeightMM = Mathf.Clamp(desiredTotalH - TopHeightMM - BottomHeightMM,
				MidHeightMM_Min, MidHeightMM_Max);
			_diameterMM = Mathf.Clamp(Data.DimensionsMM.x, DiameterMM_Min, DiameterMM_Max);

			transform.localScale = Vector3.one;
			Data.DimensionsMM = new Vector3Int(_diameterMM, TotalHeightMM, _diameterMM);

			float toU = AppConstants.MM_TO_UNITS;
			float outerR = _diameterMM * 0.5f * toU;
			float midR = MidDiameterMM * 0.5f * toU;
			float topH = TopHeightMM * toU;
			float midH = _midHeightMM * toU;
			float bottomH = BottomHeightMM * toU;

			var meshFilter = GetComponent<MeshFilter>();
			if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

			var mesh = PillarMesh.Build(outerR, midR, outerR, topH, midH, bottomH);
			AdoptOwnedMesh(mesh);
			meshFilter.sharedMesh = mesh;

			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

			ElementRoot.UseMeshCollider(gameObject, mesh);
		}

		public override Vector3[] GetVertices() => GetVerticesAt(transform.position);

		public override Vector3[] GetVerticesAt(Vector3 position)
		{
			float toU = AppConstants.MM_TO_UNITS;
			float halfSize = _diameterMM * 0.5f * toU;
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
			float w = _diameterMM * toU;
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
