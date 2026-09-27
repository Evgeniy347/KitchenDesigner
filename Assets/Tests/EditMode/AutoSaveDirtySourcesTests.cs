using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Audio;

/// <summary>f6ce82db made autosave skip a tick while SceneRevision had not moved. But the
/// save file also carries project state that never touches SceneRevision — settings, project
/// notes, the global lights switch, music, levels — so an edit to only those would sit unsaved
/// until the next scene change. Every such source now moves ProjectDirty, and the gate reads
/// SceneRevision + ProjectDirty through AutoSaveManager.ProjectRevision(). One test per source,
/// each also checking that a no-op write does NOT mark the project (or idle ticks would
/// serialise again). The camera is deliberately not a source: orbiting is not an edit.</summary>
public class AutoSaveDirtySourcesTests
{
    private string _notes = "";
    private bool _lights;
    private int _track;
    private int _volume;
    private int _gridStep;
    private Level[] _levels = new Level[0];

    [SetUp]
    public void SetUp()
    {
        _notes = ProjectInstructions.Text;
        _lights = LightSourceElement.GlobalOn;
        _track = MusicState.Track;
        _volume = MusicState.VolumePct;
        _gridStep = KitchenSettings.Instance.GridStep;
        _levels = new System.Collections.Generic.List<Level>(LevelRegistry.Items).ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        ProjectInstructions.Text = _notes;
        LightSourceElement.SetGlobalOn(_lights);
        MusicState.Track = _track;
        MusicState.VolumePct = _volume;
        KitchenSettings.Instance.GridStep = _gridStep;
        LevelRegistry.Set(_levels);
    }

    [Test]
    public void ProjectNotes_Edit_MovesTheRevision_SameTextDoesNot()
    {
        AssertMarks(() => ProjectInstructions.Text = _notes + " edited", "project notes");
        AssertQuiet(() => ProjectInstructions.Text = ProjectInstructions.Text, "same notes text");
    }

    [Test]
    public void GlobalLights_Toggle_MovesTheRevision_SameStateDoesNot()
    {
        AssertMarks(() => LightSourceElement.SetGlobalOn(!_lights), "lights switch");
        AssertQuiet(() => LightSourceElement.SetGlobalOn(LightSourceElement.GlobalOn), "same lights state");
    }

    [Test]
    public void Music_TrackOrVolume_MovesTheRevision_SameValueDoesNot()
    {
        AssertMarks(() => MusicState.VolumePct = _volume == 10 ? 20 : 10, "music volume");
        AssertMarks(() => MusicState.Track = MusicState.Track + 1, "music track");
        AssertQuiet(() => MusicState.VolumePct = MusicState.VolumePct, "same volume");
    }

    [Test]
    public void Levels_Add_MovesTheRevision()
    {
        AssertMarks(() => LevelRegistry.Add(new Level("autosave-test", "Test", 3000, 2800)), "levels");
    }

    [Test]
    public void Settings_Change_MovesTheRevision_UntouchedSettingsDoNot()
    {
        AssertQuiet(() => { }, "untouched settings");
        AssertMarks(() => KitchenSettings.Instance.GridStep = _gridStep + 1, "settings");
    }

    private static void AssertMarks(System.Action edit, string source)
    {
        int before = AutoSaveManager.ProjectRevision();
        edit();
        Assert.AreNotEqual(before, AutoSaveManager.ProjectRevision(),
            $"{source} is written to the save file, so changing it must make autosave serialise");
    }

    private static void AssertQuiet(System.Action edit, string source)
    {
        int before = AutoSaveManager.ProjectRevision();
        edit();
        Assert.AreEqual(before, AutoSaveManager.ProjectRevision(),
            $"{source}: nothing changed, so an idle autosave tick must stay free");
    }
}
