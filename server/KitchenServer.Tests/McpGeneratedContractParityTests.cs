using System.Globalization;
using System.Text;
using McpContractGen;

namespace KitchenServer.Tests;

public class McpGeneratedContractParityTests
{
    private const int MinimumToolsExpected = 40;

    [Fact]
    public void ToolsGeneratedTs_MatchesTheContract_WithoutRunningTheGenerator()
    {
        var path = LocateGeneratedFile();
        var committed = Normalize(File.ReadAllText(path));
        var fresh = Normalize(McpContractEmitter.Emit());

        Assert.True(committed == fresh, DescribeDrift(path, committed, fresh));
    }

    [Fact]
    public void GeneratedFileProbe_ResolvesARealToolTable_NotAnEmptyOrMissingFile()
    {
        var path = LocateGeneratedFile();
        var committed = Normalize(File.ReadAllText(path));

        Assert.Contains("export const GEN_TOOLS: GenTool[] = [", committed, StringComparison.Ordinal);
        Assert.True(
            McpContractEmitter.ToolCount >= MinimumToolsExpected,
            $"Контракт отдал всего {McpContractEmitter.ToolCount} инструментов. " +
            "Сравнение с пустым реестром сошлось бы само собой, и сторож перестал бы " +
            "что-либо стеречь — сначала почини реестр.");
        Assert.Equal(
            McpContractEmitter.ToolCount,
            CountToolNames(committed));
    }

    private static int CountToolNames(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (line.StartsWith("    name: \"", StringComparison.Ordinal)) count++;
        return count;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string LocateGeneratedFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                McpContractEmitter.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Не нашёл {McpContractEmitter.OutputRelativePath}, поднимаясь вверх от " +
            $"{AppContext.BaseDirectory}. Если файл переехал — почини путь здесь, иначе " +
            "сторож генератора молча перестанет проверять что бы то ни было.");
    }

    private static string DescribeDrift(string path, string committed, string fresh)
    {
        var committedLines = committed.Split('\n');
        var freshLines = fresh.Split('\n');
        var sb = new StringBuilder();

        sb.Append(path)
          .Append(" отстал от контракта Assets/Scripts/Core/MCP/Contract/**. Почини так: ")
          .Append(McpContractEmitter.RegenerateCommand)
          .Append(" (в mcp-server/), затем закоммить файл.\n")
          .Append(CultureInfo.InvariantCulture, $"строк в файле: {committedLines.Length}, у генератора: {freshLines.Length}\n");

        var shown = 0;
        for (var i = 0; i < Math.Max(committedLines.Length, freshLines.Length) && shown < 10; i++)
        {
            var a = i < committedLines.Length ? committedLines[i] : "<нет строки>";
            var b = i < freshLines.Length ? freshLines[i] : "<нет строки>";
            if (a == b) continue;
            shown++;
            sb.Append(CultureInfo.InvariantCulture, $"строка {i + 1}\n  в файле:     {a}\n  у генератора: {b}\n");
        }

        return sb.ToString();
    }
}
