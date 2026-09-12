using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IAutoSeated
    {
        void SeatAfterMove(IReadOnlyList<KitchenElement> scene, SnapCursor cursor = default);

        void RepairJoint(IReadOnlyList<KitchenElement> scene);
    }
}
