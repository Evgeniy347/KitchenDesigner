using System.Collections.Generic;

namespace KitchenDesigner.Core.Ports
{
    public readonly struct LegFrame
    {
        public static readonly LegFrame None = new LegFrame(System.Array.Empty<PipeAxis>());
        public static readonly LegFrame OneWay = new LegFrame(new[] { PipeAxis.Up });
        public static readonly LegFrame TwoWayStraight =
            new LegFrame(new[] { PipeAxis.Down, PipeAxis.Up });
        public static readonly LegFrame TwoWayCorner =
            new LegFrame(new[] { PipeAxis.Down, PipeAxis.Right });
        public static readonly LegFrame ThreeWay =
            new LegFrame(new[] { PipeAxis.Down, PipeAxis.Up, PipeAxis.Right });

        private readonly PipeAxis[] _axes;

        private LegFrame(PipeAxis[] axes)
        {
            _axes = axes;
        }

        public IReadOnlyList<PipeAxis> Axes => _axes;

        public int Count => _axes.Length;

        public PipeAxis this[int index] => _axes[index];
    }
}
