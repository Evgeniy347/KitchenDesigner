using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class SofaRig
    {
        private const int UnitsPerMetreMM = 1000;

        private readonly Transform _front;
        private readonly Transform _hinge;
        private readonly FurniturePartSet _seat;
        private readonly FurniturePartSet _cushions;
        private readonly FurniturePartSet _backrest;
        private Vector3 _hingeMM;

        public SofaRig(Transform owner)
        {
            _front = NewPivot(owner, SofaLayout.FrontGroupName);
            _hinge = NewPivot(owner, SofaLayout.HingeGroupName);
            _seat = new FurniturePartSet(_front, true);
            _cushions = new FurniturePartSet(_front, false, true);
            _backrest = new FurniturePartSet(_hinge, true);
        }

        public MeshRenderer? SeatRenderer => _seat.RendererOf(SofaLayout.SeatName);

        public void Build(Vector3Int dimensionsMM, int seatHeightMM, int cornerRadiusMM)
        {
            _hingeMM = SofaUnfold.HingeMM(dimensionsMM, seatHeightMM);
            _hinge.localPosition = _hingeMM * AppConstants.MM_TO_UNITS;

            _seat.Place(new[] { SofaLayout.Seat(dimensionsMM, seatHeightMM, cornerRadiusMM) });
            _cushions.Place(SofaLayout.Cushions(dimensionsMM, seatHeightMM));
            var backrest = SofaLayout.Backrest(dimensionsMM, cornerRadiusMM);
            _backrest.Place(new[] { backrest.WithCentre(backrest.CentreMM - _hingeMM) });
        }

        public void ApplyPose(SofaPose pose)
        {
            _front.localPosition = new Vector3(0f, 0f, pose.SeatSlideMM * AppConstants.MM_TO_UNITS);
            _hinge.localPosition = (_hingeMM + new Vector3(0f, 0f, pose.BackrestShiftMM))
                * AppConstants.MM_TO_UNITS;
            _hinge.localRotation = Quaternion.Euler(pose.BackrestAngleDeg, 0f, 0f);
            _cushions.SetVisible(pose.CushionsOnSeat);
        }

        public void ApplyTiling(Vector3Int dimensionsMM, Vector2Int primaryTileMM,
            Vector2Int secondaryTileMM)
        {
            var seatSurface = SofaLayout.SeatSurfaceMM(dimensionsMM);
            var seat = SeatRenderer;
            if (seat != null)
                MaterialManager.SetTiling(seat, MaterialManager.ComputeTileST(seatSurface,
                    primaryTileMM.x, primaryTileMM.y));

            var backrestSurface = new Vector2Int(dimensionsMM.x, SofaLayout.BackrestHeightMM);
            var backrestTiling = MaterialManager.ComputeTileST(backrestSurface,
                primaryTileMM.x, primaryTileMM.y);
            _backrest.ForEachRenderer(r => MaterialManager.SetTiling(r, backrestTiling));

            var metre = new Vector2Int(UnitsPerMetreMM, UnitsPerMetreMM);
            var cushionTiling = MaterialManager.ComputeTileST(metre,
                secondaryTileMM.x, secondaryTileMM.y);
            _cushions.ForEachRenderer(r => MaterialManager.SetTiling(r, cushionTiling));
        }

        public void SetUpholstery(Material material)
        {
            _seat.SetMaterial(material);
            _backrest.SetMaterial(material);
        }

        public void SetCushions(Material material) => _cushions.SetMaterial(material);

        public void Destroy()
        {
            _seat.Destroy();
            _cushions.Destroy();
            _backrest.Destroy();
            DestroyPivot(_front);
            DestroyPivot(_hinge);
        }

        private static Transform NewPivot(Transform owner, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(owner, false);
            return pivot;
        }

        private static void DestroyPivot(Transform pivot)
        {
            if (pivot == null) return;
            var go = pivot.gameObject;
            pivot.SetParent(null, false);
            DestroyNow.The(go);
        }
    }
}
