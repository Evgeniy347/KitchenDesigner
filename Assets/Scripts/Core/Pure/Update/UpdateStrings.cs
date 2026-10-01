using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public static class UpdateStrings
    {
        public const float TransientSeconds = 3f;

        public static string CheckError => Loc.T("update.checkError");
        public static string UpToDate => Loc.T("update.upToDate");
        public static string DownloadError => Loc.T("update.downloadError");
        public static string DownloadCancelled => Loc.T("update.cancelled");

        public static string UpdateTitle => Loc.T("update.available.title");
        public static string UpdateMessage => Loc.T("update.available.message");
        public static string UpdateAcceptButton => Loc.T("update.available.accept");
        public static string UpdateCancelButton => Loc.T("common.cancel");

        public static string DownloadTitle => Loc.T("update.download.title");
        public static string DownloadMessage => Loc.T("update.download.message");
        public static string DownloadCancelButton => Loc.T("common.cancel");
        public static string RetryAttempt => Loc.T("update.download.retry");
    }

    public enum StatusLevel { Info, Success, Warning, Error }
}
