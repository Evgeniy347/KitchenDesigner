namespace KitchenDesigner.Core.MCP
{
    public sealed class McpImageReply
    {
        public const string PngMimeType = "image/png";

        public readonly string Text;
        public readonly byte[] Png;

        public McpImageReply(string text, byte[] png)
        {
            Text = text;
            Png = png;
        }
    }
}
