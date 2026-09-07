using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public interface IPipeSceneSnapshot
    {
        IReadOnlyList<PipePort> Ports();

        IReadOnlyList<PipeSegment> Segments();

        IReadOnlyList<PipeObstacle> Obstacles();
    }
}
