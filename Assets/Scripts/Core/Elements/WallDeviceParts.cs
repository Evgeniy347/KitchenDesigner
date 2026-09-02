using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class WallDeviceParts
    {
        private readonly KitchenElement _owner;
        private FurniturePartSet? _body;
        private FurniturePartSet? _accent;

        public WallDeviceParts(KitchenElement owner) => _owner = owner;

        public FurniturePartSet Body => _body ??= new FurniturePartSet(_owner.transform);

        public FurniturePartSet Accent => _accent ??= new FurniturePartSet(_owner.transform);

        public Vector3Int Resize(IWallDevice device)
        {
            var dims = WallDeviceLayout.DimensionsMM(device.PlateWidthMM, device.PlateHeightMM,
                device.ProtrusionMM, device.PostCount);

            _owner.transform.localScale = Vector3.one;
            _owner.Data.DimensionsMM = dims;
            ApplianceCollider.FitBox(_owner.gameObject,
                new Vector3(dims.x, dims.y, dims.z), Vector3.zero);
            return dims;
        }

        public void Place(IReadOnlyList<FurniturePartBox> bodyParts,
            IReadOnlyList<FurniturePartBox> accentParts)
        {
            Body.Place(bodyParts);
            Accent.Place(accentParts);
        }

        public void Destroy()
        {
            _body?.Destroy();
            _accent?.Destroy();
        }
    }
}
