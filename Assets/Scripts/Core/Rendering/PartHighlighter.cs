using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PartHighlighter
    {
        private static string _shownRegion = string.Empty;

        public static bool IsShown(KitchenElement element, string region) =>
            element != null && HighlightOverlay.ShownFor == element
            && HighlightOverlay.PieceCount > 0
            && string.Equals(_shownRegion, region, StringComparison.Ordinal);

        public static string PipeEndRegion(PartEnd end) => "pipe:" + end;

        public static string FittingMouthRegion(int portIndex) => "mouth:" + portIndex;

        public static void ShowPipeEnd(PipeElement pipe, PartEnd end)
        {
            if (pipe == null) return;
            Show(pipe, PipeEndRegion(end),
                () => PipePartHighlight.PipeEnd(pipe.LengthMM, pipe.OuterDiameterMm, end),
                () => ShowPipeEnd(pipe, end));
        }

        public static void ShowFittingMouth(PipeFittingElement fitting, int portIndex)
        {
            if (fitting == null) return;
            Show(fitting, FittingMouthRegion(portIndex),
                () => PipePartHighlight.FittingMouth(fitting.NodeKind, fitting.PortFrameSizeId,
                    fitting.BoreSizeIds, portIndex),
                () => ShowFittingMouth(fitting, portIndex));
        }

        public static void Sync() => HighlightOverlay.Sync();

        public static void Hide() => HighlightOverlay.Hide();

        private static void Show(KitchenElement element, string region,
            Func<HighlightSleeve> geometry, Action rebuild)
        {
            Hide();
            if (element == null) return;

            var sleeveMM = geometry();
            if (sleeveMM.IsEmpty) return;
            if (HighlightOverlay.HighlightMaterial() == null) return;

            HighlightOverlay.AddSleeve(element.transform, sleeveMM);
            if (HighlightOverlay.PieceCount == 0) return;

            _shownRegion = region;
            HighlightOverlay.Begin(element, rebuild, () => Signature(element, geometry()), Forget);
        }

        private static int Signature(KitchenElement element, HighlightSleeve sleeveMM) =>
            new HighlightSignature()
                .Add(element.transform.position)
                .Add(element.transform.rotation)
                .Add(element.transform.lossyScale)
                .Add(sleeveMM)
                .Value;

        private static void Forget() => _shownRegion = string.Empty;
    }
}
