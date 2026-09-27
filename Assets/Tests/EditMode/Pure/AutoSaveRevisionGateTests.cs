using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Autosave serialised the whole project on EVERY interval, changed or not: on the
/// user's 412-element project that was ~130 ms of main-thread work per tick for nothing
/// (after the ProjectJsonTrim fix; 2.4 s before it). The gate asks SceneRevision first — the
/// counter every command, undo/redo, registration and pose change already bumps — and only a
/// moved revision pays for capture + serialise. Counted in serialisations, not ms.</summary>
public class AutoSaveRevisionGateTests
{
    private sealed class Counter
    {
        public int Serialisations;
        public int Writes;
        public bool WriteSucceeds = true;
        public string Scene = "v1";

        public string Capture()
        {
            Serialisations++;
            return Scene;
        }

        public bool Write(string json)
        {
            Writes++;
            return WriteSucceeds;
        }
    }

    [Test]
    public void Run_RevisionUnchanged_SerialisesZeroTimes_OverManyTicks()
    {
        var gate = new AutoSaveRevisionGate(savedRevision: 7);
        var scene = new Counter();
        string last = "v1";
        for (int tick = 0; tick < 10; tick++)
            last = gate.Run(7, scene.Capture, scene.Write, last).LastSavedJson;

        Assert.AreEqual(0, scene.Serialisations, "an idle project must not be serialised by autosave at all");
        Assert.AreEqual(0, scene.Writes);
    }

    [Test]
    public void Run_OneChange_SerialisesExactlyOnce_AndWritesOnce_AcrossLaterIdleTicks()
    {
        var gate = new AutoSaveRevisionGate(savedRevision: 7);
        var scene = new Counter { Scene = "v2" };
        string last = "v1";
        for (int tick = 0; tick < 5; tick++)
            last = gate.Run(8, scene.Capture, scene.Write, last).LastSavedJson;

        Assert.AreEqual(1, scene.Serialisations, "one edit = one serialisation, not one per tick");
        Assert.AreEqual(1, scene.Writes);
        Assert.AreEqual("v2", last);
    }

    [Test]
    public void Run_RevisionMovedButSnapshotSame_SerialisesOnce_AndThenGoesQuiet()
    {
        var gate = new AutoSaveRevisionGate(savedRevision: 1);
        var scene = new Counter();
        gate.Run(2, scene.Capture, scene.Write, "v1");
        gate.Run(2, scene.Capture, scene.Write, "v1");

        Assert.AreEqual(1, scene.Serialisations,
            "a bump that changed nothing on disk is settled by one comparison, not re-checked every tick");
        Assert.AreEqual(0, scene.Writes);
    }

    [Test]
    public void Run_FailedWrite_RetriesOnTheNextTick()
    {
        var gate = new AutoSaveRevisionGate(savedRevision: 1);
        var scene = new Counter { Scene = "v2", WriteSucceeds = false };
        var first = gate.Run(2, scene.Capture, scene.Write, "v1");
        scene.WriteSucceeds = true;
        var second = gate.Run(2, scene.Capture, scene.Write, first.LastSavedJson);

        Assert.IsFalse(first.Wrote);
        Assert.IsTrue(second.Wrote, "a failed write must not mark the revision saved, or the edit is lost");
        Assert.AreEqual(2, scene.Writes);
    }

    [Test]
    public void MarkSaved_AfterAManualSave_SuppressesTheNextAutosave()
    {
        var gate = new AutoSaveRevisionGate(savedRevision: 1);
        var scene = new Counter { Scene = "v2" };
        gate.MarkSaved(5);
        gate.Run(5, scene.Capture, scene.Write, "v1");

        Assert.AreEqual(0, scene.Serialisations, "Ctrl+S already wrote revision 5; autosave must not redo it");
    }
}
