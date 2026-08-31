using System;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Единая точка правды для всех текстов, которые видит пользователь в процессе
    /// автообновления, и для констант показa. Держим отдельным классом, чтобы
    /// (а) формулировки можно было править в одном месте, не трогая логику,
    /// (б) тесты сравнивали строки по этим же константам, а не хардкодом.
    /// Формулировки — user-friendly: без «отказа системы», с объяснением что делать.
    /// </summary>
    public static class UpdateStrings
    {
        /// <summary>Сколько висит одноразовое сообщение в статус-баре.StatusBarUI
        /// дополнительно клампит к своему MinSeconds (3с), так что фактически это
        /// «не меньше трёх секунд».</summary>
        public const float TransientSeconds = 3f;

        // ── Статус-бар (коротко, по делу, без жаргона) ─────────────────────
        public const string CheckError = "Не удалось проверить обновления";
        public const string UpToDate = "Обновлений нет — установлена актуальная версия";
        public const string DownloadError = "Не удалось скачать обновление";
        public const string DownloadCancelled = "Обновление отменено";

        // ── Диалог «есть новая версия» ────────────────────────────────────
        public const string UpdateTitle = "Доступно обновление";
        // {0} — номер новой версии.
        public const string UpdateMessage =
            "Доступна новая версия Kitchen Designer {0}.\n\n" +
            "Обновить сейчас? Приложение сохранит работу, установит обновление и " +
            "перезапустится автоматически.";
        public const string UpdateAcceptButton = "Обновить и перезапустить";
        public const string UpdateCancelButton = "Отмена";

        // ── Окно загрузки ─────────────────────────────────────────────────
        public const string DownloadTitle = "Установка обновления";
        // {0} — номер новой версии.
        public const string DownloadMessage =
            "Загружаем Kitchen Designer {0}…\n\n" +
            "После завершения загрузки приложение будет автоматически перезапущено.";
        public const string DownloadCancelButton = "Отмена";
    }

    /// <summary>Уровень одноразового сообщения в статус-баре — влияет на цвет.</summary>
    public enum StatusLevel { Info, Success, Error }
}
