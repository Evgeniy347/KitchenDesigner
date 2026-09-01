#nullable enable
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Общая обвязка генераторов картинок для docs/ (см. tools\artifacts.ps1).
///
/// Раньше каждый генератор поднимал сцену сам, и обвязка разъезжалась: у одного
/// UI композитился одной камерой, у другого — двумя; окружение никто не задавал
/// вовсе. Итог был виден в docs/: в overview.png пол и габариты светильников
/// перекрывали левую панель — почему именно, разобрано у AttachUiOverlay.
///
/// Второе, что чинится здесь же — освещение. Окружение (ambient) в пустой сцене
/// тест-раннера не совпадает с Assets/Scenes/TestScene.unity, а SunController
/// пишет ambientLight, который при режиме Skybox просто игнорируется. Поэтому
/// окружение задаётся явно и одинаково для всех генераторов.</summary>
public abstract class DocsArtifactFixture
{
    protected const int RenderW = 1920;
    protected const int RenderH = 1080;

    /// <summary>Фон кадра там, где не видно неба. Тот же холодный светло-серый,
    /// что и в приложении.</summary>
    protected static readonly Color Background = new Color(0.85f, 0.87f, 0.9f, 1f);

    /// <summary>Ближнее отсечение и расстояние до плоскости UI-канвы в кадрах с
    /// интерфейсом. Обоснование обоих чисел — у <see cref="AttachUiOverlay"/>.</summary>
    private const float UiNearClipM = 0.6f;
    private const float UiPlaneDistanceM = 0.9f;

    private GameObject? _bootstrap;
    private GameObject? _cameraGo;
    private GameObject? _sunGo;
    private Canvas[] _canvases = new Canvas[0];
    private RenderMode[] _canvasModes = new RenderMode[0];

    private AmbientMode _prevAmbientMode;
    private Color _prevAmbientSky, _prevAmbientEquator, _prevAmbientGround;
    private float _prevAmbientIntensity;

    protected Camera Cam => _cameraGo!.GetComponent<Camera>();
    protected Light Sun => _sunGo!.GetComponent<Light>();

    [UnitySetUp]
    public IEnumerator DocsSetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _cameraGo = new GameObject("Main Camera");
        _cameraGo.tag = "MainCamera";
        var cam = _cameraGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;

        _sunGo = new GameObject("Directional Light");
        var light = _sunGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        RenderSettings.sun = light;

        SnapshotAmbient();
        ApplyAmbient();
        SunController.Reset();

        SaveLoadManager.LastPath = "";
        var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        if (File.Exists(autoPath)) File.Delete(autoPath);

        _bootstrap = new GameObject("Bootstrap");
        _bootstrap.AddComponent<Bootstrap>();

        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator DocsTearDown()
    {
        RestoreCanvases();

        // example.save.json несёт handleMode="Move" → RestoreScene выставил
        // глобальный статик. Возвращаем дефолт, иначе тулбар «Ручки: перенос»
        // течёт в снапшоты последующих фикстур (Iso*, тулбар).
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        KitchenSettings.Instance.NormalView.edgeOutline = false;
        KitchenSettings.Instance.NormalView.wallOutline = true;

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null) Object.Destroy(c.gameObject);

        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_cameraGo != null) Object.Destroy(_cameraGo);
        if (_sunGo != null) Object.Destroy(_sunGo);

        RestoreAmbient();
        yield return null;
    }

    // ── Окружение ────────────────────────────────────────────────────────

    /// <summary>Ambient задаётся явно, а не наследуется от пустой сцены
    /// тест-раннера. Trilight вместо Skybox: у Skybox-режима в batch-mode нет
    /// запечённого пробника, и грани, смотрящие вверх, уезжали в чистый белый —
    /// столешницы на старых кадрах были засвечены до потери формы.</summary>
    private void ApplyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.55f, 0.60f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.23f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
        DynamicGI.UpdateEnvironment();
    }

    private void SnapshotAmbient()
    {
        _prevAmbientMode = RenderSettings.ambientMode;
        _prevAmbientSky = RenderSettings.ambientSkyColor;
        _prevAmbientEquator = RenderSettings.ambientEquatorColor;
        _prevAmbientGround = RenderSettings.ambientGroundColor;
        _prevAmbientIntensity = RenderSettings.ambientIntensity;
    }

    private void RestoreAmbient()
    {
        RenderSettings.ambientMode = _prevAmbientMode;
        RenderSettings.ambientSkyColor = _prevAmbientSky;
        RenderSettings.ambientEquatorColor = _prevAmbientEquator;
        RenderSettings.ambientGroundColor = _prevAmbientGround;
        RenderSettings.ambientIntensity = _prevAmbientIntensity;
    }

    // ── Сцена ────────────────────────────────────────────────────────────

    /// <summary>Загрузить docs/example.save.json и закрыть все окна проекта:
    /// их состав задаёт тест, а не сейв (см. ProjectWindowsTestState).</summary>
    protected IEnumerator LoadExampleProject()
    {
        string savePath = Path.Combine(Application.dataPath, "..", "docs", "example.save.json");
        Assert.IsTrue(File.Exists(savePath), $"Save file not found: {savePath}");

        var data = SaveLoadManager.LoadFromFile(savePath);
        Assert.IsNotNull(data, "Failed to deserialize save file");

        SaveLoadManager.ClearBoards(PartRegistry.GetAll());
        var created = SaveLoadManager.RestoreScene(data!);
        Assert.IsTrue(created.Count > 0, "No elements created from save");

        ProjectWindowsTestState.ShowOnly(null);

        // Выделение тоже приезжает из сейва — вместе с подсветкой выделенной
        // детали. Кадр для документации показывает сцену, а не то, на чём стоял
        // курсор в момент сохранения.
        if (SelectionManager.Instance != null) SelectionManager.Instance.DeselectAll();

        // Контуры рёбер и стен в сейве включены, а рисует их EdgeOutlineRenderer
        // через GL.LINES шейдером Hidden/GridLine — мимо буфера глубины и уже
        // ПОСЛЕ канвы. Никакая плоскость UI их не перекрывает: рёбра деталей,
        // уходящих за левую панель (холодильник и то, что за кадром слева),
        // тянулись линиями поверх панели и тулбара. Проверено кадром: выключить
        // одни только контуры стен недостаточно, линии возвращаются.
        // Кадрам БЕЗ интерфейса контуры включает сам генератор.
        KitchenSettings.Instance.NormalView.edgeOutline = false;
        KitchenSettings.Instance.NormalView.wallOutline = false;

        yield return null;
        yield return null;
        yield return null;
    }

    protected static KitchenElement FindPart(string partName)
    {
        foreach (var el in PartRegistry.GetAll())
            if (el != null && el.PartName == partName) return el;
        Assert.Fail($"Part '{partName}' not found in {nameof(PartRegistry)}");
        return null!;
    }

    /// <summary>Крупный план: орбита вокруг конкретной точки сцены.
    ///
    /// Ставить камеру напрямую (transform.position + LookAt) нельзя, хотя это и
    /// выглядит проще: CameraController в своём Update каждый кадр пересчитывает
    /// позицию из target/углов/дистанции, и к моменту съёмки от ручной установки
    /// не остаётся ничего — кадр уезжает туда, где орбита стояла в сейве.
    /// Поэтому крупный план тоже задаётся орбитой.</summary>
    protected IEnumerator FrameOrbit(Vector3 target, float angleX, float angleY,
        float distance, float fieldOfView)
    {
        Cam.fieldOfView = fieldOfView;
        yield return ApplyCameraState(new CameraState
        {
            valid = true,
            targetX = target.x, targetY = target.y, targetZ = target.z,
            angleX = angleX, angleY = angleY, distance = distance
        });
    }

    protected IEnumerator ApplyCameraState(CameraState state)
    {
        if (CameraController.Instance != null)
            CameraController.Instance.SetState(state);
        yield return null;
    }

    // ── UI ───────────────────────────────────────────────────────────────

    /// <summary>Подмешать интерфейс в кадр.
    ///
    /// В приложении канва работает в режиме ScreenSpaceOverlay и рисуется прямо
    /// в бэкбуфер — поверх ВСЕГО, безусловно. В RenderTexture она так не
    /// попадает вовсе, приходится переключать её на ScreenSpaceCamera, а это
    /// уже обычная геометрия перед камерой, которая соревнуется со сценой по
    /// глубине. Отсюда обе поломки старого overview.png: при planeDistance по
    /// умолчанию (100 м) канва уезжала ЗА сцену и пол выедал из левой панели
    /// куски, а на близкой плоскости её перечёркивали чёрные бруски контура
    /// прозрачной стены — комната всего 3,2 м в ширину, и при обзорном ракурсе
    /// объектив стоит от западной стены в четверти метра.
    ///
    /// Решают это две плоскости сразу. Ближнее отсечение поднимается до
    /// <see cref="UiNearClipM"/> — стена вместе со своим контуром отрезается
    /// целиком, а до ближайшей ПОЛЕЗНОЙ геометрии в наших ракурсах метры.
    /// Плоскость канвы ставится сразу за ним: ближе неё в кадре уже ничего не
    /// остаётся, а масштаб канвы держится крупным — SDF-текст TextMeshPro при
    /// микроскопическом масштабе просто не рисуется, на пробном кадре с 0,075 м
    /// остались фон кнопок и иконки, а все подписи исчезли.
    ///
    /// Отдельной UI-камерой это не решается: канвы приложения создаются на слое
    /// Default (UIFactory.CreateCanvas), и вторая камера перерисовала бы поверх
    /// кадра ещё и всю сцену. Два прохода одной камерой (сцена, затем UI с
    /// очисткой глубины) тоже не работают: URP не сохраняет цвет между двумя
    /// Camera.Render() — второй проход отдал пустой фон, на котором остались
    /// только линии контуров и интерфейс.</summary>
    protected IEnumerator AttachUiOverlay()
    {
        Cam.nearClipPlane = UiNearClipM;

        _canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        _canvasModes = new RenderMode[_canvases.Length];
        for (int i = 0; i < _canvases.Length; i++)
        {
            _canvasModes[i] = _canvases[i].renderMode;
            _canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            _canvases[i].worldCamera = Cam;
            _canvases[i].planeDistance = UiPlaneDistanceM;
        }

        yield return null;
    }

    private void RestoreCanvases()
    {
        for (int i = 0; i < _canvases.Length; i++)
        {
            if (_canvases[i] == null) continue;
            _canvases[i].renderMode = _canvasModes[i];
            _canvases[i].worldCamera = null;
        }
        _canvases = new Canvas[0];
        _canvasModes = new RenderMode[0];
    }

    /// <summary>Спрятать весь UI: для кадров, где интерфейс не нужен.</summary>
    protected static void HideAllUi()
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null && c.transform.parent == null) c.gameObject.SetActive(false);
    }

    // ── Кадр ─────────────────────────────────────────────────────────────

    /// <summary>Отрисовать кадр в RenderTexture и записать PNG в docs/.</summary>
    protected IEnumerator CaptureToDocs(string fileName, int width = RenderW, int height = RenderH)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Cam.targetTexture = rt;
        yield return null;
        yield return null;


        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string outDir = Path.Combine(Application.dataPath, "..", "docs");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, fileName);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());

        RenderTexture.active = null;
        Cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        Assert.IsTrue(File.Exists(outPath), $"PNG was not created at {outPath}");
        Assert.Greater(new FileInfo(outPath).Length, 0, "PNG file is empty");
        Debug.Log($"[docs] Saved: {outPath} ({new FileInfo(outPath).Length} bytes)");
    }
}
