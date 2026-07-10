using KitchenDesigner.Core.MCP;

/// <summary>
/// Shared configuration for PlayMode tests. Call <see cref="ConfigureForTests"/>
/// at the very beginning of each [UnitySetUp] to avoid conflicts with the
/// running editor instance (e.g. separate MCP port, disabled autosave, etc.).
/// </summary>
public static class PlayModeTestConfig
{
    /// <summary>
    /// Non-default TCP port used by the in-game MCP bridge during PlayMode tests.
    /// This keeps tests isolated from the editor instance that usually owns port 9337.
    /// </summary>
    public const int TestMcpPort = 19337;

    /// <summary>
    /// Must be called before any Bootstrap is created so that the auto-created
    /// <see cref="UnityTcpBridge"/> picks the test port in its Awake().
    /// </summary>
    public static void ConfigureForTests()
    {
        UnityTcpBridge.TestPort = TestMcpPort;
    }
}
