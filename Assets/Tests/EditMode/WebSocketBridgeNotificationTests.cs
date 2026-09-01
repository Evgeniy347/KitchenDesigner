using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class WebSocketBridgeNotificationTests
{
    [Test]
    public void LockTakenPush_IsRecognisedAsAServerNotification()
    {
        Assert.IsTrue(WebSocketBridge.IsServerPushedNotification("{\"type\":\"lock_taken\"}"),
            "сервер сам толкает lock_taken — это уведомление, а не MCP-запрос; "
            + "разбор его как запроса вернул бы клиенту Parse error вместо закрытия вкладки");
    }

    [Test]
    public void OrdinaryMcpRequest_IsNotMistakenForAServerNotification()
    {
        Assert.IsFalse(WebSocketBridge.IsServerPushedNotification(
                "{\"id\":\"1\",\"method\":\"tools/list\"}"),
            "обычный запрос обязан дойти до McpCommandHandler, а не гасить вкладку");
    }
}
