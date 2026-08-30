using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal readonly struct AABB
    {
        public readonly float minX, minY, minZ;
        public readonly float maxX, maxY, maxZ;

        public AABB(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            this.minX = minX; this.minY = minY; this.minZ = minZ;
            this.maxX = maxX; this.maxY = maxY; this.maxZ = maxZ;
        }
    }
    public static class FacadeValidator
    {
        public const float DefaultMaxFaceObstructionDepthMm = 100f;

        public const int DefaultOpeningTrajectorySteps = 12;

        public const float MinOverlapMm = Tolerance.ContactMm;

        public static Vector3 GetFaceNormal(FacadeElement facade)
        {
            if (facade == null) return Vector3.zero;
            return facade.transform.rotation * Vector3.forward;
        }

        public static bool IsFacingInward(FacadeElement facade)
        {
            if (facade == null) return false;
            var module = GroupManager.GroupOf(facade);
            if (module == null) return false;

            var members = GroupManager.MembersOf(module);
            Vector3 othersCenter = Vector3.zero;
            int count = 0;
            foreach (var m in members)
            {
                if (m == null || m == facade) continue;
                othersCenter += m.transform.position;
                count++;
            }
            if (count == 0) return false;

            othersCenter /= count;
            var toOthers = othersCenter - facade.transform.position;
            return Vector3.Dot(toOthers, GetFaceNormal(facade)) > 0.001f;
        }

        public static List<FaceObstruction> FindFaceObstructions(
            FacadeElement facade,
            List<KitchenElement> all,
            float maxDepthMm = DefaultMaxFaceObstructionDepthMm)
        {
            var result = new List<FaceObstruction>();
            if (facade == null || all == null || all.Count == 0) return result;

            var face = GetFrontFace(facade);
            var exclude = BuildExclusionSet(facade);
            float maxDepthUnits = maxDepthMm * AppConstants.MM_TO_UNITS;

            foreach (var other in all)
            {
                if (other == null || other == facade || exclude.Contains(other)) continue;

                var otherAabb = ComputeAABB(other.GetVertices());
                if (!IsInFrontOfPlane(face, otherAabb, maxDepthUnits, out float distanceUnits)) continue;

                if (!ProjectedOverlap(face, otherAabb, out float overlapWidthUnits, out float overlapHeightUnits)) continue;

                float toMm = 1f / AppConstants.MM_TO_UNITS;
                if (overlapWidthUnits * toMm < MinOverlapMm || overlapHeightUnits * toMm < MinOverlapMm) continue;

                result.Add(new FaceObstruction
                {
                    neighbor = other.PartName,
                    distanceFromFaceMm = distanceUnits * toMm,
                    overlapWidthMm = overlapWidthUnits * toMm,
                    overlapHeightMm = overlapHeightUnits * toMm
                });
            }

            return result;
        }

        public static List<OpeningViolation> FindOpeningViolations(
            FacadeElement facade,
            List<KitchenElement> all,
            int steps = DefaultOpeningTrajectorySteps)
        {
            var result = new List<OpeningViolation>();
            if (facade == null || all == null || all.Count == 0) return result;

            var exclude = BuildExclusionSet(facade);
            var half = facade.transform.localScale * 0.5f;
            var closedPos = facade.ClosedPosition;
            var closedRot = facade.ClosedRotation;
            var mode = facade.Mode;

            var others = new List<KitchenElement>();
            foreach (var other in all)
                if (other != null && other != facade && !exclude.Contains(other))
                    others.Add(other);
            if (others.Count == 0) return result;

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            float progressStep = 1f / Mathf.Max(1, steps);

            for (int i = 1; i <= steps; i++)
            {
                float progress = i * progressStep;
                FacadeDoor.Pose(closedPos, closedRot, half, mode, progress, out var pos, out var rot);
                var sweptAabb = ComputeSweptAABB(closedPos, closedRot, half, pos, rot);

                foreach (var other in others)
                {
                    var otherAabb = ComputeAABB(other.GetVertices());
                    if (!AABBsOverlap(sweptAabb, otherAabb)) continue;

                    float overlap = AABBOverlapVolume(sweptAabb, otherAabb);
                    if (overlap * toMm < MinOverlapMm) continue;

                    result.Add(new OpeningViolation
                    {
                        neighbor = other.PartName,
                        openingMode = ModeSymbol(mode),
                        collisionAtProgress = progress,
                        collisionOverlapMm = overlap * toMm
                    });
                    others.Remove(other);
                    break;
                }
                if (others.Count == 0) break;
            }

            return result;
        }

        public struct FrontFace
        {
            public Vector3 center;
            public Vector3 normal;
            public Vector3 right;
            public Vector3 up;
            public Vector2 size;
        }

        public struct FaceObstruction
        {
            public string neighbor;
            public float distanceFromFaceMm;
            public float overlapWidthMm;
            public float overlapHeightMm;
        }

        public struct OpeningViolation
        {
            public string neighbor;
            public string openingMode;
            public float collisionAtProgress;
            public float collisionOverlapMm;
        }

        private static FrontFace GetFrontFace(FacadeElement facade)
        {
            var t = facade.transform;
            var face = facade.GetFaces()[4];
            return new FrontFace
            {
                center = face.center,
                normal = face.normal,
                right = face.rightAxis,
                up = face.upAxis,
                size = face.size
            };
        }

        private static HashSet<KitchenElement> BuildExclusionSet(FacadeElement facade)
        {
            var set = new HashSet<KitchenElement>();
            if (facade == null) return set;

            var module = GroupManager.GroupOf(facade);
            if (module != null)
                foreach (var m in GroupManager.MembersOf(module))
                    if (m != null && m != facade)
                        set.Add(m);

            foreach (Transform child in facade.transform)
            {
                var childEl = child.GetComponent<KitchenElement>();
                if (childEl != null) set.Add(childEl);
            }

            return set;
        }

        private static bool IsInFrontOfPlane(FrontFace face, AABB aabb, float maxDepthUnits, out float distanceUnits)
        {
            distanceUnits = float.MaxValue;
            var corners = AABBCorners(aabb);
            bool anyInFront = false;

            foreach (var c in corners)
            {
                float d = Vector3.Dot(c - face.center, face.normal);
                if (d >= 0f)
                {
                    anyInFront = true;
                    if (d < distanceUnits) distanceUnits = d;
                }
            }

            if (!anyInFront) return false;
            return distanceUnits <= maxDepthUnits;
        }

        private static bool ProjectedOverlap(FrontFace face, AABB aabb, out float overlapWidthUnits, out float overlapHeightUnits)
        {
            overlapWidthUnits = 0f;
            overlapHeightUnits = 0f;

            var corners = AABBCorners(aabb);
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;

            foreach (var c in corners)
            {
                var local = c - face.center;
                float u = Vector3.Dot(local, face.right);
                float v = Vector3.Dot(local, face.up);
                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            float halfW = face.size.x * 0.5f;
            float halfH = face.size.y * 0.5f;
            float interLeft = Mathf.Max(minU, -halfW);
            float interRight = Mathf.Min(maxU, halfW);
            float interBottom = Mathf.Max(minV, -halfH);
            float interTop = Mathf.Min(maxV, halfH);

            if (interRight <= interLeft || interTop <= interBottom) return false;

            overlapWidthUnits = interRight - interLeft;
            overlapHeightUnits = interTop - interBottom;
            return true;
        }

        private static AABB ComputeSweptAABB(Vector3 aPos, Quaternion aRot, Vector3 aHalf, Vector3 bPos, Quaternion bRot)
        {
            var vertsA = TransformedBoxVertices(aPos, aRot, aHalf);
            var vertsB = TransformedBoxVertices(bPos, bRot, aHalf);
            return ComputeAABB(vertsA, vertsB);
        }

        private static Vector3[] TransformedBoxVertices(Vector3 pos, Quaternion rot, Vector3 half)
        {
            var local = new Vector3[]
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z),
            };
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = pos + rot * local[i];
            return result;
        }

        private static AABB ComputeAABB(Vector3[] a, Vector3[] b)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in a) Expand(v, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            foreach (var v in b) Expand(v, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static AABB ComputeAABB(Vector3[] verts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in verts) Expand(v, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static void Expand(Vector3 v, ref float minX, ref float minY, ref float minZ, ref float maxX, ref float maxY, ref float maxZ)
        {
            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.x > maxX) maxX = v.x;
            if (v.y > maxY) maxY = v.y;
            if (v.z > maxZ) maxZ = v.z;
        }

        private static Vector3[] AABBCorners(AABB aabb)
        {
            return new Vector3[]
            {
                new Vector3(aabb.minX, aabb.minY, aabb.minZ),
                new Vector3(aabb.maxX, aabb.minY, aabb.minZ),
                new Vector3(aabb.minX, aabb.maxY, aabb.minZ),
                new Vector3(aabb.maxX, aabb.maxY, aabb.minZ),
                new Vector3(aabb.minX, aabb.minY, aabb.maxZ),
                new Vector3(aabb.maxX, aabb.minY, aabb.maxZ),
                new Vector3(aabb.minX, aabb.maxY, aabb.maxZ),
                new Vector3(aabb.maxX, aabb.maxY, aabb.maxZ),
            };
        }

        private static bool AABBsOverlap(AABB a, AABB b)
        {
            return a.minX < b.maxX && a.maxX > b.minX &&
                   a.minY < b.maxY && a.maxY > b.minY &&
                   a.minZ < b.maxZ && a.maxZ > b.minZ;
        }

        private static float AABBOverlapVolume(AABB a, AABB b)
        {
            float ox = Mathf.Min(a.maxX, b.maxX) - Mathf.Max(a.minX, b.minX);
            float oy = Mathf.Min(a.maxY, b.maxY) - Mathf.Max(a.minY, b.minY);
            float oz = Mathf.Min(a.maxZ, b.maxZ) - Mathf.Max(a.minZ, b.minZ);
            if (ox <= 0f || oy <= 0f || oz <= 0f) return 0f;
            return Mathf.Min(ox, oy, oz);
        }

        private static string ModeSymbol(DoorMode mode)
        {
            return FacadeDoor.WireName(mode);
        }
    }
}
