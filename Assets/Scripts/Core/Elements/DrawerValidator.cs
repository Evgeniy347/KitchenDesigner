using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DrawerValidator
    {
        public const float MIN_WALL_THICKNESS_MM = 16f;
        // AABB ящика — контурный бокс проёма: зазор направляющих 37.5 мм на сторону
        // уже ВНУТРИ бокса. Правильная установка — контур вплотную к боковинам
        // корпуса; допускаем люфт до 12.5 мм (реальный зазор до короба 37.5–50 мм).
        public const float MAX_CLEARANCE_PER_SIDE_MM = 12.5f;
        public const float MIN_CLEARANCE_PER_SIDE_MM = 0f;

        public struct DrawerValidationResult
        {
            public bool IsValid;
            public List<string> Errors;

            public static DrawerValidationResult Ok() => new DrawerValidationResult { IsValid = true, Errors = new List<string>() };
            public static DrawerValidationResult Fail(string error) => new DrawerValidationResult { IsValid = false, Errors = new List<string> { error } };

            public void AddError(string error)
            {
                IsValid = false;
                Errors.Add(error);
            }
        }

        public static DrawerValidationResult ValidateSideWalls(DrawerElement drawer, List<KitchenElement> allElements)
        {
            if (drawer == null || allElements == null || allElements.Count == 0)
                return DrawerValidationResult.Fail("Нет данных для проверки");

            var result = DrawerValidationResult.Ok();
            var drawerAabb = ComputeAABB(drawer.GetVertices());

            float minWallThicknessUnits = MIN_WALL_THICKNESS_MM * AppConstants.MM_TO_UNITS;
            float toMm = 1f / AppConstants.MM_TO_UNITS;

            KitchenElement? leftWall = null;
            KitchenElement? rightWall = null;
            float leftGap = float.MaxValue;
            float rightGap = float.MaxValue;

            foreach (var el in allElements)
            {
                if (el == null || el == drawer) continue;

                var otherAabb = ComputeAABB(el.GetVertices());
                float wallThicknessX = otherAabb.maxX - otherAabb.minX;

                if (wallThicknessX < minWallThicknessUnits - 0.0001f) continue;

                // Стенка обязана перекрывать ящик по высоте и глубине — иначе любой
                // элемент слева/справа в другом конце сцены считался бы «стенкой».
                if (!OverlapsYZ(otherAabb, drawerAabb)) continue;

                if (otherAabb.maxX <= drawerAabb.minX)
                {
                    float gap = drawerAabb.minX - otherAabb.maxX;
                    if (gap < leftGap)
                    {
                        leftGap = gap;
                        leftWall = el;
                    }
                }

                if (otherAabb.minX >= drawerAabb.maxX)
                {
                    float gap = otherAabb.minX - drawerAabb.maxX;
                    if (gap < rightGap)
                    {
                        rightGap = gap;
                        rightWall = el;
                    }
                }
            }

            if (leftWall == null)
            {
                result.AddError("Нет левой стенки");
            }
            else
            {
                float clearanceMm = leftGap * toMm;
                if (clearanceMm < MIN_CLEARANCE_PER_SIDE_MM - 0.1f || clearanceMm > MAX_CLEARANCE_PER_SIDE_MM + 0.1f)
                    result.AddError($"Зазор слева {clearanceMm:F1} мм (допуск {MIN_CLEARANCE_PER_SIDE_MM}-{MAX_CLEARANCE_PER_SIDE_MM} мм)");
            }

            if (rightWall == null)
            {
                result.AddError("Нет правой стенки");
            }
            else
            {
                float clearanceMm = rightGap * toMm;
                if (clearanceMm < MIN_CLEARANCE_PER_SIDE_MM - 0.1f || clearanceMm > MAX_CLEARANCE_PER_SIDE_MM + 0.1f)
                    result.AddError($"Зазор справа {clearanceMm:F1} мм (допуск {MIN_CLEARANCE_PER_SIDE_MM}-{MAX_CLEARANCE_PER_SIDE_MM} мм)");
            }

            return result;
        }

        public static DrawerValidationResult ValidateCabinetFit(DrawerElement drawer, List<KitchenElement> allElements)
        {
            var result = DrawerValidationResult.Ok();
            if (drawer == null || allElements == null) return result;
            if (!drawer.IsDouble) return result;

            // AABB двойного ящика считаем по паре (PairedDrawerName), а НЕ по группе
            // GroupManager: ящик штатно живёт в группе модуля вместе с корпусом, и
            // групповая AABB совпала бы с самим корпусом — валидация сравнивала бы
            // корпус сам с собой.
            var drawerGroupMembers = new List<KitchenElement> { drawer };
            if (!string.IsNullOrEmpty(drawer.PairedDrawerName))
            {
                foreach (var el in allElements)
                    if (el is DrawerElement d && d != drawer && d.PartName == drawer.PairedDrawerName)
                    {
                        drawerGroupMembers.Add(d);
                        break;
                    }
            }

            AABB groupAabb = default;
            bool first = true;
            foreach (var m in drawerGroupMembers)
            {
                if (m == null) continue;
                var aabb = ComputeAABB(m.GetVertices());
                if (first) { groupAabb = aabb; first = false; }
                else groupAabb = Union(groupAabb, aabb);
            }
            if (first) return result;

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            float groupHeightMm = (groupAabb.maxY - groupAabb.minY) * toMm;

            float topY = float.MaxValue;
            float bottomY = float.MinValue;

            foreach (var el in allElements)
            {
                if (el == null || drawerGroupMembers.Contains(el)) continue;
                var aabb = ComputeAABB(el.GetVertices());

                // Верх/дно корпуса обязаны перекрывать ящик в плане (X/Z).
                if (!OverlapsXZ(aabb, groupAabb)) continue;

                if (aabb.minY >= groupAabb.maxY - 0.0001f && aabb.minY < topY)
                    topY = aabb.minY;

                if (aabb.maxY <= groupAabb.minY + 0.0001f && aabb.maxY > bottomY)
                    bottomY = aabb.maxY;
            }

            if (topY < float.MaxValue && bottomY > float.MinValue)
            {
                float availableHeightMm = (topY - bottomY) * toMm;
                if (groupHeightMm > availableHeightMm + 1f)
                    result.AddError($"Двойной ящик ({groupHeightMm:F0} мм) не входит в корпус ({availableHeightMm:F0} мм)");
            }
            else
            {
                if (topY >= float.MaxValue)
                    result.AddError("Не найден верх корпуса для двойного ящика");
                if (bottomY <= float.MinValue)
                    result.AddError("Не найдено дно корпуса для двойного ящика");
            }

            return result;
        }

        public static DrawerValidationResult ValidateAll(DrawerElement drawer, List<KitchenElement> allElements)
        {
            var result = ValidateSideWalls(drawer, allElements);
            var cabinetResult = ValidateCabinetFit(drawer, allElements);
            foreach (var err in cabinetResult.Errors)
                result.AddError(err);
            return result;
        }

        private static AABB ComputeAABB(Vector3[] verts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in verts)
            {
                if (v.x < minX) minX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.z < minZ) minZ = v.z;
                if (v.x > maxX) maxX = v.x;
                if (v.y > maxY) maxY = v.y;
                if (v.z > maxZ) maxZ = v.z;
            }
            return new AABB(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static bool OverlapsYZ(AABB a, AABB b)
        {
            return a.maxY > b.minY && a.minY < b.maxY
                && a.maxZ > b.minZ && a.minZ < b.maxZ;
        }

        private static bool OverlapsXZ(AABB a, AABB b)
        {
            return a.maxX > b.minX && a.minX < b.maxX
                && a.maxZ > b.minZ && a.minZ < b.maxZ;
        }

        private static AABB Union(AABB a, AABB b)
        {
            return new AABB(
                Mathf.Min(a.minX, b.minX), Mathf.Min(a.minY, b.minY), Mathf.Min(a.minZ, b.minZ),
                Mathf.Max(a.maxX, b.maxX), Mathf.Max(a.maxY, b.maxY), Mathf.Max(a.maxZ, b.maxZ)
            );
        }
    }
}
