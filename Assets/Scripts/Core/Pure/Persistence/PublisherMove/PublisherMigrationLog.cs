using System;

namespace KitchenDesigner.Core
{
    internal sealed class PublisherMigrationLog
    {
        public PublisherMigrationLog(Action<string> info, Action<string> warning, Action<string> error)
        {
            Info = info;
            Warning = warning;
            Error = error;
        }

        public Action<string> Info { get; }
        public Action<string> Warning { get; }
        public Action<string> Error { get; }
    }
}
