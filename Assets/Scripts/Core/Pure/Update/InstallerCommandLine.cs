using System.IO;

namespace KitchenDesigner.Core.Update
{
    public static class InstallerCommandLine
    {
        public const string SilentRelaunchSwitches = "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH";

        public static string LogPathFor(string installerPath) => Path.ChangeExtension(installerPath, ".log");

        public static string ForSilentRelaunch(string installerPath) =>
            SilentRelaunchSwitches + " /LOG=\"" + LogPathFor(installerPath) + "\"";
    }
}
