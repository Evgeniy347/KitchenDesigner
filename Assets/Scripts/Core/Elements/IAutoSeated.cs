using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IAutoSeated
    {
        void SeatAfterMove(IReadOnlyList<KitchenElement> scene);
    }
}
