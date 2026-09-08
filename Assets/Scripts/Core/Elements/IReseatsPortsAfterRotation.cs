using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IReseatsPortsAfterRotation
    {
        object? CaptureLinksForRotation(IReadOnlyList<KitchenElement> scene);

        void ReseatAfterRotation(object? capturedLinks);
    }
}
