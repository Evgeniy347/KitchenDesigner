using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.Update
{
    public static class UpdateMessages
    {
        private const long BytesPerMegabyte = 1024 * 1024;

        public static string NoVersionInResponse => "В ответе нет номера версии";

        public static string UnreadableTag(string tag) => "Не удалось разобрать номер версии: " + tag;

        public static string NoInstallerForVersion(string version) =>
            "В релизе нет установщика x64 для версии " + version;

        public static string NewVersionFound(string version) => "обнаружена новая версия " + version;

        public static string UpToDate(string current, string latest) =>
            "установлена актуальная версия " + current + " (последний релиз " + latest + ")";

        public static string UnreadableCurrentVersion(string current) =>
            "номер установленной версии не разобран («" + current + "»): проверка обновлений пропущена";

        public static string UnreadableReleaseVersion(string version) =>
            "номер версии релиза не годится для имени установщика («" + version + "»): обновление пропущено";

        public static string CheckFailed(string reason) =>
            "не удалось проверить обновления: " + reason + "; повтор при следующем запуске";

        public static string NoRelease(string reason) => "релиз не найден: " + reason;

        public static string NoInstaller(string version) =>
            "в релизе " + version + " нет установщика x64: обновление пропущено";

        public static string ForeignInstaller(string actual, string expected) =>
            "установщик релиза называется «" + actual + "», ожидалось «" + expected
            + "»: файл другой версии не загружается";

        public static string NoDownloadUrl(string version) =>
            "у установщика версии " + version + " нет адреса загрузки: обновление пропущено";

        public static string CannotVerify(string version) =>
            "релиз " + version + " не публикует ни SHA-256, ни размер установщика: проверить файл нельзя,"
            + " установщик не будет загружен и запущен";

        public static string SizeOnlyCheck(long size) =>
            "релиз не публикует SHA-256: установщик будет проверен только по размеру (" + size + " байт)";

        public static string Cleaning(IReadOnlyList<string> files) =>
            "удаляю устаревшие файлы обновлений (" + files.Count + "): " + string.Join(", ", files);

        public static string DeleteFailed(string file, string reason) =>
            "не удалось удалить " + file + ": " + reason;

        public static string ExistingVerified(string file, IntegrityMethod method) =>
            "найден скачанный ранее установщик " + file + ", проверка пройдена (" + MethodName(method) + "): загрузка не нужна";

        public static string ExistingMissing(string file) =>
            "скачанный ранее установщик " + file + " исчез: загружаю заново";

        public static string ExistingBroken(string file, IntegrityMethod method) =>
            "скачанный ранее установщик " + file + " не прошёл проверку (" + MethodName(method)
            + "): удаляю и загружаю заново";

        public static string DownloadStarted(string version, int attempt) =>
            "начинаю загрузку установщика версии " + version + " (попытка " + attempt + ")";

        public static string DownloadFinished(string file) =>
            "загрузка " + file + " завершена, проверяю целостность";

        public static string DownloadFailed(string reason) =>
            "загрузка не удалась: " + reason + "; повтор при следующем запуске";

        public static string DownloadRetry(int attempt, int attempts, string reason) =>
            "загрузка прервалась (" + reason + "), попытка " + attempt + " из " + attempts;

        public static string Progress(long received, long total)
        {
            if (total <= 0) return "загружено " + Megabytes(received) + " МБ";
            long percent = received >= total ? 100 : received * 100 / total;
            return "загрузка: " + percent.ToString(CultureInfo.InvariantCulture) + "% ("
                + Megabytes(received) + " из " + Megabytes(total) + " МБ)";
        }

        public static string DownloadedVerified(IntegrityMethod method) =>
            "загруженный файл прошёл проверку (" + MethodName(method) + ")";

        public static string DownloadMismatchRetry(IntegrityMethod method) =>
            "загруженный файл не прошёл проверку (" + MethodName(method) + "): загружаю повторно";

        public static string DownloadMismatchTwice(IntegrityMethod method) =>
            "загруженный файл дважды не прошёл проверку (" + MethodName(method)
            + "): обновление отложено до следующего запуска";

        public static string PromoteFailed(string reason) =>
            "не удалось сохранить загруженный установщик: " + reason + "; повтор при следующем запуске";

        public static string Ready(string version) =>
            "установщик версии " + version + " загружен и проверен";

        public static string UserAccepted(string file) =>
            "обновление принято, проверяю " + file + " перед запуском";

        public static string UserDeclined(string file) =>
            "обновление отложено, установщик сохранён: " + file;

        public static string ChangedBeforeApply(string file) =>
            "установщик " + file + " изменился после проверки: запуск отменён, файл удалён";

        public static string Starting(string file) => "запускаю установщик " + file;

        public static string UnexpectedEvent(string name, UpdatePhase phase) =>
            "внутренняя ошибка: событие " + name + " в состоянии " + phase;

        public static string FolderUnreadable(string reason) =>
            "не удалось прочитать папку обновлений: " + reason;

        public static string DownloadFolderFailed(string reason) =>
            "не удалось подготовить папку обновлений: " + reason;

        public static string UnexpectedFailure(string reason) => "сбой обновления: " + reason;

        public static string MethodName(IntegrityMethod method) => method switch
        {
            IntegrityMethod.Sha256 => "SHA-256",
            IntegrityMethod.Size => "размер",
            _ => "проверка невозможна",
        };

        private static string Megabytes(long bytes) =>
            (bytes / BytesPerMegabyte).ToString(CultureInfo.InvariantCulture);
    }
}
