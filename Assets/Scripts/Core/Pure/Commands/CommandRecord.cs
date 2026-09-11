using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [Serializable]
    public class CommandRecord
    {
        public string type = string.Empty;
        public string description = string.Empty;
        public int elementIndex = -1;
        public float[] posBefore = new float[0];
        public float[] posAfter = new float[0];
        public float[] rotBefore = new float[0];
        public float[] rotAfter = new float[0];
        public int[] dimsBefore = new int[0];
        public int[] dimsAfter = new int[0];
        public CommandRecord[]? children;
        public int convertTo = -1;
        public ElementData[] convertState = new ElementData[0];

        public static float[] V3(Vector3 v) => new[] { v.x, v.y, v.z };
        public static float[] V4(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
        public static int[] VI(Vector3Int v) => new[] { v.x, v.y, v.z };

        public static Vector3 ToV3(float[] a) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
        public static Quaternion ToQuat(float[] a) =>
            a != null && a.Length >= 4 ? new Quaternion(a[0], a[1], a[2], a[3]) : Quaternion.identity;
        public static Vector3Int ToVI(int[] a) =>
            a != null && a.Length >= 3 ? new Vector3Int(a[0], a[1], a[2]) : Vector3Int.one;
    }
}
