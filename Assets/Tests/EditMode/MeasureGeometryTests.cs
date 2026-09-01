using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;

/// <summary>Математика рулетки: проекция на ось, определение оси отрезка,
/// «помощь попадания» по вершине и по отрезку, формат подписи.</summary>
public class MeasureGeometryTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;

    [TearDown]
    public void Teardown() => MeasureMode.Reset();

    // ── Проекция на доминирующую ось ────────────────────────────────────

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksX()
    {
        var anchor = new Vector3(1f, 2f, 3f);
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, anchor + new Vector3(0.5f, 0.2f, 0.1f));
        Assert.AreEqual(new Vector3(1.5f, 2f, 3f), end);
    }

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksY()
    {
        var anchor = new Vector3(1f, 2f, 3f);
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, anchor + new Vector3(-0.1f, -0.7f, 0.3f));
        Assert.AreEqual(new Vector3(1f, 1.3f, 3f), end);
    }

    [Test]
    public void MeasureGeometry_ProjectOnDominantAxis_PicksZ()
    {
        var anchor = Vector3.zero;
        var end = MeasureGeometry.ProjectOnDominantAxis(anchor, new Vector3(0.2f, 0.3f, -0.9f));
        Assert.AreEqual(new Vector3(0f, 0f, -0.9f), end);
    }

    // ── Ось отрезка ─────────────────────────────────────────────────────

    [TestCase(1f, 0f, 0f, 0)]
    [TestCase(0f, 1f, 0f, 1)]
    [TestCase(0f, 0f, 1f, 2)]
    public void MeasureGeometry_AxisOf_ReturnsAxisForAlignedSegment(float dx, float dy, float dz, int expected)
    {
        Assert.AreEqual(expected, MeasureGeometry.AxisOf(Vector3.zero, new Vector3(dx, dy, dz)));
    }

    [Test]
    public void MeasureGeometry_AxisOf_ReturnsMinusOneForDiagonal()
    {
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, new Vector3(1f, 1f, 0f)));
    }

    [Test]
    public void MeasureGeometry_AxisOf_ReturnsMinusOneForDegenerate()
    {
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, Vector3.zero));
    }

    /// <summary>Отклонение мельче геометрического шума (0.1 мм) — отрезок всё
    /// ещё считается осевым: вершины деталей приходят с float-погрешностью.</summary>
    [Test]
    public void MeasureGeometry_AxisOf_TreatsSubEpsilonDriftAsAligned()
    {
        var b = new Vector3(1f, Tolerance.EpsilonUnits * 0.5f, 0f);
        Assert.AreEqual(0, MeasureGeometry.AxisOf(Vector3.zero, b));
    }

    [Test]
    public void MeasureGeometry_AxisOf_TreatsAboveEpsilonDriftAsDiagonal()
    {
        var b = new Vector3(1f, Tolerance.EpsilonUnits * 2f, 0f);
        Assert.AreEqual(-1, MeasureGeometry.AxisOf(Vector3.zero, b));
    }

    // ── Помощь попадания ────────────────────────────────────────────────

    [Test]
    public void MeasureGeometry_NearestIndex_PicksClosestInsideRadius()
    {
        var points = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(4, 0) };
        Assert.AreEqual(2, MeasureGeometry.NearestIndex(points, new Vector2(5, 0), 18f));
    }

    [Test]
    public void MeasureGeometry_NearestIndex_ReturnsMinusOneOutsideRadius()
    {
        var points = new List<Vector2> { new Vector2(0, 0) };
        Assert.AreEqual(-1, MeasureGeometry.NearestIndex(points, new Vector2(50, 0), 18f));
    }

    [Test]
    public void MeasureGeometry_NearestIndex_ReturnsMinusOneForEmptyList()
    {
        Assert.AreEqual(-1, MeasureGeometry.NearestIndex(new List<Vector2>(), Vector2.zero, 18f));
    }

    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_ProjectsInside()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(50, 7));
        Assert.AreEqual(7f, d, 0.001f);
    }

    /// <summary>За торцом отрезка расстояние считается до конца, а не до прямой —
    /// иначе клик далеко за концом «попадал» бы в отрезок.</summary>
    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_ClampsBeyondEnd()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(0, 0), new Vector2(100, 0), new Vector2(130, 40));
        Assert.AreEqual(50f, d, 0.001f);
    }

    [Test]
    public void MeasureGeometry_DistancePointToSegmentPx_HandlesDegenerateSegment()
    {
        float d = MeasureGeometry.DistancePointToSegmentPx(
            new Vector2(5, 5), new Vector2(5, 5), new Vector2(5, 9));
        Assert.AreEqual(4f, d, 0.001f);
    }

    // ── Подпись ─────────────────────────────────────────────────────────

    [Test]
    public void MeasureGeometry_FormatMm_RoundsToWholeMillimetres()
    {
        Assert.AreEqual("718 мм", MeasureGeometry.FormatMm(717.6f * Mm, axisAligned: true));
    }

    [Test]
    public void MeasureGeometry_FormatMm_PrefixesAngleGlyphForDiagonal()
    {
        string text = MeasureGeometry.FormatMm(500f * Mm, axisAligned: false);
        StringAssert.StartsWith(KitchenDesigner.Core.UI.UIStyle.GlyphAngle, text);
        StringAssert.EndsWith("500 мм", text);
    }

    // ── Режим и хранилище ───────────────────────────────────────────────

    [Test]
    public void MeasureSegment_LengthMm_UsesMillimetres()
    {
        var seg = new MeasureSegment(Vector3.zero, new Vector3(0.6f, 0f, 0f));
        Assert.AreEqual(600f, seg.LengthMm, 0.001f);
        Assert.AreEqual(0, seg.Axis);
    }

    [Test]
    public void MeasureMode_SetActiveFalse_ClearsSegments()
    {
        MeasureMode.SetActive(true);
        MeasureStore.Add(new MeasureSegment(Vector3.zero, Vector3.right));
        MeasureStore.Select(MeasureStore.Segments[0]);

        MeasureMode.SetActive(false);

        Assert.AreEqual(0, MeasureStore.Segments.Count);
        Assert.IsNull(MeasureStore.Selected);
    }

    /// <summary>В URP Camera.current внутри OnRenderObject бывает null — тогда
    /// рендерер обязан взять Camera.main, иначе разметка не рисуется вообще
    /// и на экране остаётся только подпись расстояния.</summary>
    [Test]
    public void MeasureRenderer_ResolveCamera_FallsBackToMainWhenCurrentIsNull()
    {
        var go = new GameObject("MeasureCam");
        try
        {
            var main = go.AddComponent<Camera>();
            Assert.AreSame(main, MeasureRenderer.ResolveCamera(null, main));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void MeasureRenderer_ResolveCamera_PrefersCurrent()
    {
        var currentGo = new GameObject("Current");
        var mainGo = new GameObject("Main");
        try
        {
            var current = currentGo.AddComponent<Camera>();
            var main = mainGo.AddComponent<Camera>();
            Assert.AreSame(current, MeasureRenderer.ResolveCamera(current, main));
        }
        finally
        {
            Object.DestroyImmediate(currentGo);
            Object.DestroyImmediate(mainGo);
        }
    }

    [Test]
    public void MeasureStore_Remove_DropsSelection()
    {
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        MeasureStore.Add(seg);
        MeasureStore.Select(seg);

        MeasureStore.Remove(seg);

        Assert.AreEqual(0, MeasureStore.Segments.Count);
        Assert.IsNull(MeasureStore.Selected);
    }

    // ── Пиксельный размер в мире ────────────────────────────────────────

    [Test]
    public void MeasureGeometry_WorldSizeForPixels_PerspectiveCamera_GrowsProportionallyWithDistance()
    {
        var go = new GameObject("PerspectiveCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;

            float near = MeasureGeometry.WorldSizeForPixels(cam, new Vector3(0f, 0f, 2f), 10f);
            float far = MeasureGeometry.WorldSizeForPixels(cam, new Vector3(0f, 0f, 4f), 10f);

            Assert.Greater(near, 0f, "предусловие: на конечном расстоянии размер положителен");
            Assert.AreEqual(2f * near, far, near * 1e-3f,
                "вдвое дальше — вдвое крупнее в мире, и ровно поэтому на ЭКРАНЕ штрих "
                + "пунктира и точка замера остаются одного размера при отдалении камеры");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MeasureGeometry_WorldSizeForPixels_OrthographicCamera_IgnoresDistance()
    {
        var go = new GameObject("OrthographicCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;

            float near = MeasureGeometry.WorldSizeForPixels(cam, new Vector3(0f, 0f, 2f), 10f);
            float far = MeasureGeometry.WorldSizeForPixels(cam, new Vector3(0f, 0f, 400f), 10f);

            Assert.Greater(near, 0f, "предусловие: размер положителен");
            Assert.AreEqual(near, far, near * 1e-3f,
                "в ортографии экранный масштаб от расстояния не зависит — если бы здесь "
                + "работала перспективная формула, разметка «худела» бы в изометрии");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MeasureGeometry_WorldSizeForPixels_NoCamera_IsZero()
    {
        Assert.AreEqual(0f, MeasureGeometry.WorldSizeForPixels(null!, Vector3.zero, 10f),
            "рендерер зовёт это до проверки камеры — вернуть NaN значило бы разложить "
            + "разметку в мусорную геометрию вместо того, чтобы просто ничего не нарисовать");
    }

    // ── Отрезок ─────────────────────────────────────────────────────────

    [Test]
    public void MeasureSegment_Axis_AnswersTheSameAsMeasureGeometry()
    {
        var alongY = new MeasureSegment(Vector3.zero, new Vector3(0f, 0.5f, 0f));
        var diagonal = new MeasureSegment(Vector3.zero, new Vector3(0.5f, 0.5f, 0f));

        Assert.AreEqual(1, alongY.Axis, "0=X, 1=Y, 2=Z — эти числа читает подпись замера");
        Assert.AreEqual(-1, diagonal.Axis, "−1 = отрезок не лежит ни на одной оси");
        Assert.AreEqual(MeasureGeometry.AxisOf(diagonal.A, diagonal.B), diagonal.Axis,
            "у отрезка нет своей арифметики осей — иначе подпись и диагностика разошлись бы");
    }

    // ── Событие Changed у хранилища ─────────────────────────────────────

    [Test]
    public void MeasureStore_EveryChangeOfTheSetOrOfTheSelection_RaisesChanged()
    {
        MeasureStore.Clear();
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        int fired = 0;
        void Handler() => fired++;

        MeasureStore.Changed += Handler;
        try
        {
            MeasureStore.Add(seg);
            Assert.AreEqual(1, fired, "окно свойств замера перерисовывается по этому событию");

            MeasureStore.Select(seg);
            Assert.AreEqual(2, fired, "смена выбора — тоже перерисовка окна");

            MeasureStore.Remove(seg);
            Assert.AreEqual(3, fired);

            MeasureStore.Add(new MeasureSegment(Vector3.zero, Vector3.right));
            MeasureStore.Clear();
            Assert.AreEqual(5, fired, "выход из режима стирает замеры — окно обязано узнать");
        }
        finally { MeasureStore.Changed -= Handler; }
    }

    [Test]
    public void MeasureStore_ARepeatedSelectionOrAnEmptyClear_StaysSilent()
    {
        MeasureStore.Clear();
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        MeasureStore.Add(seg);
        MeasureStore.Select(seg);

        int fired = 0;
        void Handler() => fired++;

        MeasureStore.Changed += Handler;
        try
        {
            MeasureStore.Select(seg);
            Assert.AreEqual(0, fired, "выбор не менялся — перерисовывать нечего");

            MeasureStore.Remove(new MeasureSegment(Vector3.zero, Vector3.forward));
            Assert.AreEqual(0, fired, "чужого отрезка в списке нет — набор не менялся");

            MeasureStore.Clear();
            MeasureStore.Clear();
            Assert.AreEqual(1, fired, "второй Clear по пустому хранилищу молчит");
        }
        finally { MeasureStore.Changed -= Handler; }
    }

    // ── Вход и выход из режима ──────────────────────────────────────────

    [Test]
    public void MeasureMode_Enabling_TurnsTheEyedropperOff()
    {
        KitchenDesigner.Core.Tools.EyedropperMode.Reset();
        KitchenDesigner.Core.Tools.EyedropperMode.SetActive(true);

        MeasureMode.SetActive(true);

        Assert.IsFalse(KitchenDesigner.Core.Tools.EyedropperMode.Active,
            "два режима-захватчика мыши одновременно не имеют смысла — симметрично тому, "
            + "как включение пипетки гасит рулетку");
        KitchenDesigner.Core.Tools.EyedropperMode.Reset();
    }

    [Test]
    public void MeasureMode_Enabling_AfterTheSelectionManagerWasDestroyed_DoesNotThrow()
    {
        var go = new GameObject("SelectionManager");
        var manager = go.AddComponent<SelectionManager>();
        SelectionManager.Instance = manager;
        Object.DestroyImmediate(go);

        try
        {
            Assert.DoesNotThrow(() => MeasureMode.SetActive(true),
                "Instance ставится в Awake и в OnDestroy не гасится, поэтому после выгрузки "
                + "сцены здесь лежит уничтоженный объект: сравнивать его можно только "
                + "Unity-оператором !=. `?.` видит живую C#-ссылку, лезет внутрь и роняет "
                + "вход в режим MissingReferenceException");
        }
        finally { SelectionManager.Instance = null; }
    }

    [Test]
    public void MeasureMode_Changed_FiresOnEveryTransition_AndStaysSilentOnARepeat()
    {
        int fired = 0;
        void Handler() => fired++;

        MeasureMode.Changed += Handler;
        try
        {
            MeasureMode.SetActive(true);
            Assert.AreEqual(1, fired, "кнопка тулбара обязана перерисоваться на входе в режим");

            MeasureMode.SetActive(true);
            Assert.AreEqual(1, fired, "состояние не менялось — переключения не было");

            MeasureMode.SetActive(false);
            Assert.AreEqual(2, fired, "выход из режима — тоже смена состояния");
        }
        finally { MeasureMode.Changed -= Handler; }
    }

    // ── Разметка рулетки ────────────────────────────────────────────────

    [Test]
    public void MeasureRenderer_WithoutTheOverlayLineShader_WarnsInsteadOfDisappearingSilently()
    {
        string? said = null;
        var material = MeasureRenderer.BuildLineMaterialOrWarn(null, m => said = m);

        Assert.IsNull(material, "без шейдера материала нет");
        Assert.IsNotNull(said,
            "молча пропасть нельзя: без материала рулетка продолжает рисовать ПОДПИСИ, "
            + "но не линии — со стороны это выглядит как «текст в воздухе», и никто не "
            + "догадается искать пропавший шейдер");
        StringAssert.Contains("OverlayLine", said!, "в предупреждении обязано быть имя шейдера");
    }

    [Test]
    public void MeasureRenderer_WithTheShader_BuildsAMaterialThatNeverReachesTheScene()
    {
        var shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Assume.That(shader != null, "предусловие: хоть какой-то шейдер в проекте есть");

        string? said = null;
        var material = MeasureRenderer.BuildLineMaterialOrWarn(shader, m => said = m);
        try
        {
            Assert.IsNotNull(material, "положительный контроль: с шейдером материал строится");
            Assert.IsNull(said, "шейдер нашёлся — предупреждать не о чем");
            Assert.AreEqual(HideFlags.HideAndDontSave, material!.hideFlags,
                "материал служебный и создаётся в рантайме: без HideAndDontSave он попал "
                + "бы в сцену и в сохранение проекта");
        }
        finally { if (material != null) Object.DestroyImmediate(material); }
    }

    [Test]
    public void MeasureRenderer_TheSelectionTube_IsDrawnBeforeTheDashesAndThePoints()
    {
        CollectionAssert.AreEqual(
            new[] { MeasureRenderer.DrawPass.SelectionTube, MeasureRenderer.DrawPass.DashedLines,
                    MeasureRenderer.DrawPass.Points },
            MeasureRenderer.DrawOrder,
            "полупрозрачная жёлтая обойма выделения идёт ПЕРВОЙ: рисуй её после пунктира — "
            + "заливка затёрла бы красные штрихи внутри себя, и выбранный замер выглядел бы "
            + "стёртым ровно в тот момент, когда его выбрали");
    }

    [Test]
    public void MeasureRenderer_TheLine_IsThickerThanASinglePixel()
    {
        Assert.Greater(MeasureRenderer.LineThicknessPx, 1f,
            "отрезок рисуется обращённой к камере полосой, а не GL.LINES: GL.LINES даёт "
            + "ровно один пиксель и на фоне деталей почти не читается");
    }
}
