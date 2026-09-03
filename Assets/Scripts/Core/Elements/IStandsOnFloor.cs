using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IStandsOnFloor
    {
        void SeatOnFloor(IReadOnlyList<KitchenElement> scene);
    }
}
