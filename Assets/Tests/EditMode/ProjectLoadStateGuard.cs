using KitchenDesigner.Core;

/// <summary>
/// Снимок ГЛОБАЛЬНОГО состояния, которое переписывает загрузка проекта.
///
/// <c>SaveLoadManager.RestoreScene</c> восстанавливает не только детали: из файла
/// приезжают блок настроек, режим ручек, выключатель света и тонировка. Тест,
/// который поднял <c>docs/example.save.json</c> и не вернул это назад, портит
/// соседей — и портит НЕПРЕДСКАЗУЕМО, потому что сам файл переписывается
/// автосохранением десктопа. Так уже ломались SceneVisibilityTests,
/// SettingsPanelUITests, SnapshotTests (эталоны ловили чужие lightsOn/tintEnabled)
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
    private readonly bool _tintEnabled;
    private readonly int _musicTrack;
    private readonly int _musicVolumePct;

    private ProjectLoadStateGuard()
    {
        var s = KitchenSettings.Instance;
        _settings = s != null ? s.ToData() : null;
        _handleMode = ResizeHandleManager.Mode;
        _lightsOn = LightSourceElement.GlobalOn;
        _tintEnabled = ElementHighlighter.TintEnabled;
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
        ElementHighlighter.TintEnabled = _tintEnabled;
        KitchenDesigner.Core.Audio.MusicState.Track = _musicTrack;
        KitchenDesigner.Core.Audio.MusicState.VolumePct = _musicVolumePct;

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }
}
