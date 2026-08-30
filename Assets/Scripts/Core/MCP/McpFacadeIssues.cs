using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal readonly struct McpFacadeIssues
    {
        public readonly Vector3 normal;
        public readonly bool faceInward;
        public readonly List<FaceObstructionInfo> obstructions;
        public readonly List<OpeningViolationInfo> openingViolations;

        public McpFacadeIssues(Vector3 normal, bool faceInward,
            List<FaceObstructionInfo> obstructions,
            List<OpeningViolationInfo> openingViolations)
        {
            this.normal = normal;
            this.faceInward = faceInward;
            this.obstructions = obstructions;
            this.openingViolations = openingViolations;
        }

        public bool Any => faceInward || obstructions.Count > 0 || openingViolations.Count > 0;

        public static McpFacadeIssues Of(FacadeElement facade, List<KitchenElement> allElements)
        {
            var rawObstructions = FacadeValidator.FindFaceObstructions(facade, allElements);
            var obstructions = new List<FaceObstructionInfo>(rawObstructions.Count);
            foreach (var o in rawObstructions)
            {
                obstructions.Add(new FaceObstructionInfo
                {
                    neighbor = o.neighbor,
                    distanceFromFaceMm = o.distanceFromFaceMm,
                    overlapWidthMm = o.overlapWidthMm,
                    overlapHeightMm = o.overlapHeightMm
                });
            }

            var rawOpening = FacadeValidator.FindOpeningViolations(facade, allElements);
            var opening = new List<OpeningViolationInfo>(rawOpening.Count);
            foreach (var v in rawOpening)
            {
                opening.Add(new OpeningViolationInfo
                {
                    neighbor = v.neighbor,
                    openingMode = v.openingMode,
                    collisionAtProgress = v.collisionAtProgress,
                    collisionOverlapMm = v.collisionOverlapMm
                });
            }

            return new McpFacadeIssues(FacadeValidator.GetFaceNormal(facade),
                FacadeValidator.IsFacingInward(facade), obstructions, opening);
        }
    }
}
