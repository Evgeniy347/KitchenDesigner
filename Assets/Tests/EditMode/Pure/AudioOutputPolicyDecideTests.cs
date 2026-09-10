using KitchenDesigner.Core.Audio;
using NUnit.Framework;

public class AudioOutputPolicyDecideTests
{
    [Test]
    public void Decide_IsFalse_WhenNothingAsksForSilence()
    {
        Assert.IsFalse(AudioOutputPolicy.Decide(testRun: false, batchMode: false, args: new string[0]));
    }

    [Test]
    public void Decide_IsTrue_WhenTestRunAlone()
    {
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: true, batchMode: false, args: new string[0]));
    }

    [Test]
    public void Decide_IsTrue_WhenBatchModeAlone()
    {
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: false, batchMode: true, args: new string[0]));
    }

    [Test]
    public void Decide_IsTrue_WhenTheArgumentAlone()
    {
        // The one combination that actually ships to a user: not batch, not a test
        // run, just the player launched with -muteAudio (see tools/smoke-test.ps1).
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: false, batchMode: false, args: new[] { "-muteAudio" }));
    }

    [Test]
    public void Decide_IsFalse_WhenTheArgumentIsAbsentAndNothingElseAsks()
    {
        Assert.IsFalse(AudioOutputPolicy.Decide(testRun: false, batchMode: false, args: new[] { "-mcpPort", "9337" }));
    }

    [Test]
    public void Decide_IsTrue_WhenTestRunAndBatchMode()
    {
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: true, batchMode: true, args: new string[0]));
    }

    [Test]
    public void Decide_IsTrue_WhenTestRunAndTheArgument()
    {
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: true, batchMode: false, args: new[] { "-muteAudio" }));
    }

    [Test]
    public void Decide_IsTrue_WhenEverythingAsksForSilence()
    {
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: true, batchMode: true, args: new[] { "-muteAudio" }));
    }

    [Test]
    public void Decide_ToleratesNullArgs()
    {
        Assert.IsFalse(AudioOutputPolicy.Decide(testRun: false, batchMode: false, args: null));
        Assert.IsTrue(AudioOutputPolicy.Decide(testRun: true, batchMode: false, args: null));
    }
}
