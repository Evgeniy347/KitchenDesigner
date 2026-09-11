using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PoseProbe
    {
        [ThreadStatic] private static int _posesMoved;

        public static int TakePosesMoved()
        {
            int n = _posesMoved;
            _posesMoved = 0;
            return n;
        }

        public static T At<T>(Transform pose, Vector3 position, Quaternion rotation, Func<T> measure)
        {
            if (pose == null) return measure();

            var savedPos = pose.position;
            var savedRot = pose.rotation;
            bool savedChanged = pose.hasChanged;
            bool moves = savedPos != position || savedRot != rotation;
            if (!moves) return measure();

            _posesMoved++;
            try
            {
                pose.SetPositionAndRotation(position, rotation);
                return measure();
            }
            finally
            {
                pose.SetPositionAndRotation(savedPos, savedRot);
                pose.hasChanged = savedChanged;
            }
        }
    }
}
