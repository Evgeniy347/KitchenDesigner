using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MoventoDrawerMesh
    {
        public const string SUFFIX_SIDE = "Боковина";
        public const string SUFFIX_FRONT = "Перед";
        public const string SUFFIX_BACK = "Задник";
        public const string SUFFIX_BOTTOM = "Дно";

        public static List<DrawerMesh.Box> ComputeBoxes(int lwMM, DrawerType type, int nlMM)
        {
            float w = lwMM, d = nlMM;
            float t = DrawerConstants.MOVENTO_BOARD_THICKNESS;
            float clr = DrawerConstants.MOVENTO_SLIDE_CLEARANCE_PER_SIDE;
            float lift = DrawerConstants.GetBottomLift(type);
            float h = DrawerConstants.GetTypeHeight(type);

            float niche = DrawerConstants.MOVENTO_BOTTOM_NICHE;
            float sideLen = d - DrawerConstants.MOVENTO_SIDE_LENGTH_INSET;
            float fbW = w - DrawerConstants.MOVENTO_FRONT_BACK_INSET;
            float backZ = d - sideLen;
            float innerX = clr + t;
            float bottomY = lift + niche;
            float panelY = bottomY + t;
            float panelH = Mathf.Max(1f, h - niche - t);

            return new List<DrawerMesh.Box>
            {
                new DrawerMesh.Box
                {
                    name = SUFFIX_SIDE + " L",
                    minMM = new Vector3(clr, lift, backZ),
                    sizeMM = new Vector3(t, h, sideLen),
                },
                new DrawerMesh.Box
                {
                    name = SUFFIX_SIDE + " R",
                    minMM = new Vector3(w - clr - t, lift, backZ),
                    sizeMM = new Vector3(t, h, sideLen),
                },
                new DrawerMesh.Box
                {
                    name = SUFFIX_FRONT,
                    minMM = new Vector3(innerX, panelY, d - t),
                    sizeMM = new Vector3(fbW, panelH, t),
                },
                new DrawerMesh.Box
                {
                    name = SUFFIX_BACK,
                    minMM = new Vector3(innerX, panelY, backZ),
                    sizeMM = new Vector3(fbW, panelH, t),
                },
                new DrawerMesh.Box
                {
                    name = SUFFIX_BOTTOM,
                    minMM = new Vector3(innerX, bottomY, backZ),
                    sizeMM = new Vector3(fbW, t, sideLen),
                },
            };
        }

        public static Mesh Build(int lwMM, DrawerType type, int nlMM)
        {
            var dims = new Vector3(
                Mathf.Max(1, lwMM),
                Mathf.Max(1, DrawerConstants.GetMinOpeningHeight(type)),
                Mathf.Max(1, nlMM));
            return DrawerMesh.BuildFromBoxes(ComputeBoxes(lwMM, type, nlMM), dims);
        }

        public static IEnumerable<AssembledFacadeMesh.Part> ComputeParts(int lwMM, DrawerType type, int nlMM)
        {
            int t = DrawerConstants.MOVENTO_BOARD_THICKNESS;
            int h = DrawerConstants.GetTypeHeight(type);
            int sideLen = Mathf.Max(1, nlMM - DrawerConstants.MOVENTO_SIDE_LENGTH_INSET);
            int fbW = Mathf.Max(1, lwMM - DrawerConstants.MOVENTO_FRONT_BACK_INSET);
            int panelH = Mathf.Max(1, h - DrawerConstants.MOVENTO_BOTTOM_NICHE - t);

            return new List<AssembledFacadeMesh.Part>
            {
                new AssembledFacadeMesh.Part { suffix = SUFFIX_SIDE, dimsMM = new Vector3Int(sideLen, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_SIDE, dimsMM = new Vector3Int(sideLen, h, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_FRONT, dimsMM = new Vector3Int(fbW, panelH, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_BACK, dimsMM = new Vector3Int(fbW, panelH, t) },
                new AssembledFacadeMesh.Part { suffix = SUFFIX_BOTTOM, dimsMM = new Vector3Int(fbW, sideLen, t) },
            };
        }
    }
}
