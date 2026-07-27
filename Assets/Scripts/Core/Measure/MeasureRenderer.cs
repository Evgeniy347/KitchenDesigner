using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Measure
{
    /// <summary>Рисует разметку рулетки через GL поверх сцены: красный пунктир,
    /// точки-вершины и прозрачный жёлтый «цилиндр» вокруг выбранного отрезка.
    /// Всё одним проходом, без GameObject'ов и CreatePrimitive — примитивы в
    /// рантайме тянут за собой коллайдеры, которые вырезает WebGL-стриппинг.</summary>
    public class MeasureRenderer : MonoBehaviour
    {
        /// <summary>Толщина линии замера, пиксели.</summary>
        private const float LineThicknessPx = 2.5f;
        /// <summary>Штрих и пробел пунктира, пиксели (постоянны при любом зуме).</summary>
        private const float DashPx = 9f;
        private const float GapPx = 6f;
        /// <summary>Радиус точки-вершины, пиксели.</summary>
        private const float PointRadiusPx = 5f;
        /// <summary>Радиус жёлтого цилиндра вокруг выбранного отрезка, пиксели.</summary>
        private const float TubeRadiusPx = 10f;
        /// <summary>Граней у цилиндра выделения.</summary>
        private const int TubeSides = 12;

        private Material? _lineMaterial;

        private void Awake()
        {
            var shader = Shader.Find("Hidden/OverlayLine");
            if (shader == null) shader = Resources.Load<Shader>("Shaders/OverlayLine");
            if (shader == null)
            {
                // Молча пропасть нельзя: без материала рулетка рисует подписи,
                // но не линии — со стороны это выглядит как «текст в воздухе».
                Debug.LogWarning("[Measure] Шейдер Hidden/OverlayLine не найден — разметка рулетки не будет видна.");
                return;
            }
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>Камера кадра. В URP <see cref="Camera.current"/> внутри
        /// OnRenderObject не гарантирована (бывает null), а от камеры зависит
        /// вся геометрия разметки — разворот точек к зрителю и перевод пикселей
        /// в мир. Поэтому падаем на Camera.main, как остальной код проекта.</summary>
        public static Camera? ResolveCamera(Camera? current, Camera? main) =>
            current != null ? current : main;

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private void OnRenderObject()
        {
            if (!MeasureMode.Active || _lineMaterial == null) return;
            var cam = ResolveCamera(Camera.current, Camera.main);
            if (cam == null) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);

            DrawTubes(cam);
            DrawLines(cam);
            DrawPoints(cam);

            GL.PopMatrix();
        }

        // Жёлтый цилиндр рисуется первым: полупрозрачная заливка не должна
        // затирать красный пунктир внутри себя.
        private void DrawTubes(Camera cam)
        {
            var selected = MeasureStore.Selected;
            if (selected == null) return;

            GL.Begin(GL.TRIANGLES);
            GL.Color(UIStyle.MeasureSelected);
            DrawTube(cam, selected.A, selected.B);
            GL.End();
        }

        private void DrawLines(Camera cam)
        {
            var ctrl = MeasureController.Instance;

            GL.Begin(GL.QUADS);
            foreach (var seg in MeasureStore.Segments)
            {
                bool accent = ctrl != null && ctrl.Hovered == seg;
                GL.Color(accent ? UIStyle.MeasureHover : UIStyle.MeasureLine);
                DrawDashed(cam, seg.A, seg.B);
            }

            if (ctrl != null && ctrl.HasPreview)
            {
                GL.Color(UIStyle.MeasureLine);
                DrawDashed(cam, ctrl.Anchor!.Value, ctrl.PreviewEnd!.Value);
            }
            GL.End();
        }

        private void DrawPoints(Camera cam)
        {
            var ctrl = MeasureController.Instance;

            GL.Begin(GL.QUADS);
            foreach (var seg in MeasureStore.Segments)
            {
                GL.Color(UIStyle.MeasureLine);
                DrawPoint(cam, seg.A);
                DrawPoint(cam, seg.B);
            }

            if (ctrl != null)
            {
                if (ctrl.Anchor.HasValue)
                {
                    GL.Color(UIStyle.MeasureLine);
                    DrawPoint(cam, ctrl.Anchor.Value);
                }
                if (ctrl.Hint.HasValue)
                {
                    // Предложенная вершина розовая и когда она станет вторым
                    // концом — пользователь видит, куда попадёт клик.
                    GL.Color(UIStyle.MeasureHint);
                    DrawPoint(cam, ctrl.Hint.Value);
                }
            }
            GL.End();
        }

        // --- Примитивы ---

        // Пунктир: длины штриха и пробела заданы в пикселях и переводятся в мир
        // по расстоянию до середины отрезка, поэтому шаг не «сжимается» при
        // отдалении камеры.
        private static void DrawDashed(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < Tolerance.EpsilonUnits) return;
            Vector3 dir = delta / length;

            float scale = MeasureGeometry.WorldSizeForPixels(cam, (a + b) * 0.5f, 1f);
            float dash = DashPx * scale;
            float step = (DashPx + GapPx) * scale;
            if (step < Tolerance.EpsilonUnits) return;

            for (float t = 0f; t < length; t += step)
            {
                Vector3 p0 = a + dir * t;
                Vector3 p1 = a + dir * Mathf.Min(t + dash, length);
                DrawThickSegment(cam, p0, p1, LineThicknessPx);
            }
        }

        // Отрезок как обращённая к камере полоса: GL.LINES даёт 1 пиксель и на
        // фоне деталей почти не читается.
        private static void DrawThickSegment(Camera cam, Vector3 a, Vector3 b, float thicknessPx)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < Tolerance.EpsilonSqr) return;

            Vector3 side = Vector3.Cross(axis.normalized, cam.transform.forward);
            if (side.sqrMagnitude < Tolerance.EpsilonSqr) return;
            side.Normalize();

            float ha = MeasureGeometry.WorldSizeForPixels(cam, a, thicknessPx * 0.5f);
            float hb = MeasureGeometry.WorldSizeForPixels(cam, b, thicknessPx * 0.5f);

            GL.Vertex(a - side * ha);
            GL.Vertex(a + side * ha);
            GL.Vertex(b + side * hb);
            GL.Vertex(b - side * hb);
        }

        // Точка — квад, всегда развёрнутый к камере и постоянного размера на
        // экране: попасть в вершину сложно, значит она должна быть заметна.
        private static void DrawPoint(Camera cam, Vector3 p)
        {
            float r = MeasureGeometry.WorldSizeForPixels(cam, p, PointRadiusPx);
            Vector3 right = cam.transform.right * r;
            Vector3 up = cam.transform.up * r;

            GL.Vertex(p - right - up);
            GL.Vertex(p - right + up);
            GL.Vertex(p + right + up);
            GL.Vertex(p + right - up);
        }

        // «Цилиндр» выделения: боковая поверхность из TubeSides четырёхугольников,
        // разложенных на треугольники. Радиус задан в пикселях, поэтому обойма
        // одинаково толстая на любом расстоянии.
        private static void DrawTube(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < Tolerance.EpsilonSqr) return;
            axis.Normalize();

            Vector3 refUp = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > Tolerance.UpDotThreshold
                ? Vector3.forward : Vector3.up;
            Vector3 u = Vector3.Cross(axis, refUp).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            float ra = MeasureGeometry.WorldSizeForPixels(cam, a, TubeRadiusPx);
            float rb = MeasureGeometry.WorldSizeForPixels(cam, b, TubeRadiusPx);

            for (int i = 0; i < TubeSides; i++)
            {
                float a0 = (float)i / TubeSides * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / TubeSides * Mathf.PI * 2f;
                Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0);
                Vector3 d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);

                Vector3 p00 = a + d0 * ra, p01 = a + d1 * ra;
                Vector3 p10 = b + d0 * rb, p11 = b + d1 * rb;

                GL.Vertex(p00); GL.Vertex(p10); GL.Vertex(p11);
                GL.Vertex(p00); GL.Vertex(p11); GL.Vertex(p01);
            }
        }
    }
}
