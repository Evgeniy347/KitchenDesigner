using KitchenDesigner.Core;

/// <summary>
/// Снимок ГЛОБАЛЬНОГО состояния, которое переписывает загрузка проекта.
///
/// <c>SaveLoadManager.RestoreScene</c> восстанавливает не только детали: из файла
/// приезжают блок настроек, режим ручек и выключатель света. Тон валидности
/// (<c>ElementHighlighter.ViolationTintVisible</c>) из файла больше не приезжает —
/// кнопки и ключа <c>tintEnabled</c> нет, — но его гасит фоторежим, и глобальный
/// статик, утёкший из соседнего набора, портит эталоны так же молча. Тест,
/// который поднял <c>docs/example.save.json</c> и не вернул это назад, портит
/// соседей — и портит НЕПРЕДСКАЗУЕМО, потому что сам файл переписывается
/// автосохранением десктопа. Так уже ломались SceneVisibilityTests,
/// SettingsPanelUITests, SnapshotTests (эталоны ловили чужой lightsOn)
/// и WallManagerTests.
///
/// Правило: набор, грузящий проект, снимает состояние в [SetUp] и возвращает
/// в [TearDown].
/// </summary>
public sealed class ProjectLoadStateGuard
{
    private readonly KitchenSettingsData? _settings;
    private readonly ResizeHandleManager.HandleMode _handleMode;
    private readonly bool _lightsOn;
    private readonly bool _violationTintVisible;
    private readonly int _musicTrack;
    private readonly int _musicVolumePct;

    private ProjectLoadStateGuard()
    {
        var s = KitchenSettings.Instance;
        _settings = s != null ? s.ToData() : null;
        _handleMode = ResizeHandleManager.Mode;
        _lightsOn = LightSourceElement.GlobalOn;
        _violationTintVisible = ElementHighlighter.ViolationTintVisible;
        _musicTrack = KitchenDesigner.Core.Audio.MusicState.Track;
        _musicVolumePct = KitchenDesigner.Core.Audio.MusicState.VolumePct;
    }

    public static ProjectLoadStateGuard Capture() => new ProjectLoadStateGuard();

    /// <summary>Вернуть всё, что мог переписать загруженный проект. Текст
    /// инструкций, комнаты и планы не восстанавливаются, а сбрасываются: они
    /// сериализуются в снапшоты соседних наборов, и пустое состояние там
    /// единственно правильное.</summary>
    public void Restore()
    {
        var s = KitchenSettings.Instance;
        if (s != null && _settings != null) s.ApplyFrom(_settings);
        ResizeHandleManager.SetMode(_handleMode);
        LightSourceElement.SetGlobalOn(_lightsOn);
        ElementHighlighter.ViolationTintVisible = _violationTintVisible;
        KitchenDesigner.Core.Audio.MusicState.Track = _musicTrack;
        KitchenDesigner.Core.Audio.MusicState.VolumePct = _musicVolumePct;

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }
}
