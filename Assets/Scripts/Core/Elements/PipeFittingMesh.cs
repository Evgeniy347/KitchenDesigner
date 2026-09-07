using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public static class PipeFittingMesh
    {
        public static Mesh Build(PipeNodeKind kind, string frameSizeId,
            IReadOnlyList<string?>? boreSizeIds)
        {
            var builder = new PlumbingMesh(Vector3.zero);
            builder.AddSegments(PipeFittingLayout.PartsMM(kind, frameSizeId, boreSizeIds));
            return builder.Build();
        }
    }
}
