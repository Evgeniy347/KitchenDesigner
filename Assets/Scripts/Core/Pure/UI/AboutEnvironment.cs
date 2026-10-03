namespace KitchenDesigner.Core.UI
{
    public readonly struct AboutEnvironment
    {
        public AboutEnvironment(string version, string buildDate, string platform, string unityVersion,
            string graphicsApi, string gpu)
        {
            Version = version;
            BuildDate = buildDate;
            Platform = platform;
            UnityVersion = unityVersion;
            GraphicsApi = graphicsApi;
            Gpu = gpu;
        }

        public string Version { get; }
        public string BuildDate { get; }
        public string Platform { get; }
        public string UnityVersion { get; }
        public string GraphicsApi { get; }
        public string Gpu { get; }
    }
}
