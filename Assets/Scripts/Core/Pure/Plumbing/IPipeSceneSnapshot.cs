using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public interface IPipeSceneSnapshot
    {
        IReadOnlyList<PipePort> Ports();

        IReadOnlyList<PipeRunSegment> Segments();

        IReadOnlyList<PipeObstacle> Obstacles();
    }
}
