using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class SnapCandidates
    {
        public readonly List<SnapCandidate> Candidates = new List<SnapCandidate>();
        public readonly List<Vector3> ZeroShiftNormals = new List<Vector3>();
        public readonly List<Vector3> AlreadyAlignedNormals = new List<Vector3>();

        public SnapResult ConfirmedContact;
        public string? ConfirmedContactLog;

        public void ClearForRefill()
        {
            Candidates.Clear();
            ZeroShiftNormals.Clear();
        }
    }
}
