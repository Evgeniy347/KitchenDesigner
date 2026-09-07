using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public static class UpdateStrings
    {
        public const float TransientSeconds = 3f;

        public const string CheckError = "Не удалось проверить обновления";
        public const string UpToDate = "Обновлений нет — установлена актуальная версия";
        public const string DownloadError = "Не удалось скачать обновление";
        public const string DownloadCancelled = "Обновление отменено";

        public const string UpdateTitle = "Доступно обновление";
        public const string UpdateMessage =
            "Доступна новая версия Kitchen Designer {0}.\n\n" +
            "Обновить сейчас? Приложение сохранит работу, установит обновление и " +
            "перезапустится автоматически.";
        public const string UpdateAcceptButton = "Обновить и перезапустить";
        public const string UpdateCancelButton = "Отмена";

        public const string DownloadTitle = "Установка обновления";
        public const string DownloadMessage =
            "Загружаем Kitchen Designer {0}…\n\n" +
            "После завершения загрузки приложение будет автоматически перезапущено.";
        public const string DownloadCancelButton = "Отмена";
        public const string RetryAttempt = "Повторная попытка {0} из {1}…";
    }

    public enum StatusLevel { Info, Success, Warning, Error }
}
