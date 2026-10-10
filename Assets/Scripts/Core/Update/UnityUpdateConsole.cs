using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public sealed class UnityUpdateConsole : IUpdateConsole
    {
        public const string Prefix = "[Update] ";

        public void Write(UpdateLogLevel level, string text)
        {
            switch (level)
            {
                case UpdateLogLevel.Warning:
                    Debug.LogWarning(Prefix + text);
                    break;
                case UpdateLogLevel.Error:
                    Debug.LogError(Prefix + text);
                    break;
                default:
                    Debug.Log(Prefix + text);
                    break;
            }
        }
    }
}
