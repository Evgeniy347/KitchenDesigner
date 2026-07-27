using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Bulk
{
    /// <summary>
    /// Эвристика «расширь модуль на N мм» (MCP v2). Агент задаёт (модуль, ось,
    /// дельта), а сервер САМ решает, какие доски растянуть, какие сдвинуть — LLM не
    /// пересчитывает координаты каждой детали.
    ///
    /// Правило (ближняя сторона фиксирована, модуль растёт в сторону +ось):
    ///   • «пролётные» доски (габарит вдоль оси ≥ spanFraction ширины модуля):
    ///     растягиваются на дельту, центр смещается на дельту/2;
    ///   • «крайние» доски в дальней половине: сдвигаются на дельту;
    ///   • доски в ближней половине: остаются на месте.
    /// Работает в мировых осях; мировая ось маппится на локальный габарит через
    /// поворот (устойчиво к rotY 0/90/180/270 — типичный случай кухни).
    /// </summary>
    public static class ModuleResize
    {
        public struct Change
        {
            public KitchenElement element;
            public Vector3 newPosition;
            public Vector3Int newDimensions;
        }

        public static List<Change> Plan(IList<KitchenElement> members, char worldAxis,
            float deltaMM, float spanFraction = 0.6f)
        {
            var changes = new List<Change>();
            if (members == null || members.Count == 0) return changes;

            Vector3 axis = AxisVector(worldAxis);
            float deltaU = deltaMM * AppConstants.MM_TO_UNITS;

            // Границы модуля вдоль оси.
            float mmin = float.MaxValue, mmax = float.MinValue;
            foreach (var e in members)
            {
                if (e == null) continue;
                Span(e, axis, out float emin, out float emax, out _);
                if (emin < mmin) mmin = emin;
                if (emax > mmax) mmax = emax;
            }
            if (mmax <= mmin) return changes;
            float mwidth = mmax - mmin;
            float mmid = (mmin + mmax) * 0.5f;

            foreach (var e in members)
            {
                if (e == null) continue;
                Span(e, axis, out float emin, out float emax, out int localAxis);
                float esize = emax - emin;
                float ecenter = (emin + emax) * 0.5f;

                bool span = esize >= spanFraction * mwidth;
                if (span)
                {
                    // Растягиваем: ближний край на месте, дальний +дельта.
                    var dims = e.DimensionsMM;
                    dims[localAxis] = Mathf.Max(1, dims[localAxis] + Mathf.RoundToInt(deltaMM));
                    changes.Add(new Change
                    {
                        element = e,
                        newPosition = e.transform.position + axis * (deltaU * 0.5f),
                        newDimensions = dims,
                    });
                }
                else if (ecenter > mmid)
                {
                    // Крайняя доска дальней половины — сдвигаем целиком.
                    changes.Add(new Change
                    {
                        element = e,
                        newPosition = e.transform.position + axis * deltaU,
                        newDimensions = e.DimensionsMM,
                    });
                }
                // Ближняя половина (не пролётная) — без изменений.
            }
            return changes;
        }

        private static Vector3 AxisVector(char worldAxis) => char.ToLowerInvariant(worldAxis) switch
        {
            'x' => Vector3.right,
            'y' => Vector3.up,
            _ => Vector3.forward,
        };

        /// <summary>Протяжённость элемента вдоль мировой оси: центр по оси и
        /// локальный габарит, чей локальный орт лучше всего совпал с осью.</summary>
        private static void Span(KitchenElement e, Vector3 axis, out float min, out float max, out int localAxis)
        {
            var t = e.transform;
            float dr = Mathf.Abs(Vector3.Dot(axis, t.right));
            float du = Mathf.Abs(Vector3.Dot(axis, t.up));
            float df = Mathf.Abs(Vector3.Dot(axis, t.forward));
            localAxis = (dr >= du && dr >= df) ? 0 : (du >= df ? 1 : 2);

            float sizeU = e.DimensionsMM[localAxis] * AppConstants.MM_TO_UNITS;
            float center = Vector3.Dot(t.position, axis);
            min = center - sizeU * 0.5f;
            max = center + sizeU * 0.5f;
        }
    }
}
