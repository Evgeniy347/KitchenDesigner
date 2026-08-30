using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Реестр инструментов против диспетчера. Шапка McpToolRegistry.cs с самого
/// начала ссылалась на этот тест, но его не существовало: 63 объявленных инструмента
/// и switch в McpCommandHandler.Handle никто не сверял. Ветки switch читаем текстом
/// исходника — прецедент GeometryArchitectureTests; путь ищем обходом вверх, потому
/// что каталог сборки у Unity лежит в Library/ScriptAssemblies проекта.</summary>
public class McpToolRegistryParityTests
{
    private static readonly Regex DispatchedMethod =
        new Regex(@"case\s+""([a-z_]+)""\s*:\s*return\s+Handle", RegexOptions.Compiled);

    private static string DispatcherSource()
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(McpToolRegistryParityTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName,
                    "Assets", "Scripts", "Core", "MCP", "McpCommandHandler.cs");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException(
            "Не найден McpCommandHandler.cs ни от одной из точек: " + string.Join(", ", roots));
    }

    [Test]
    public void Registry_AndTheDispatcherSwitch_DeclareTheSameTools()
    {
        var dispatched = DispatchedMethod.Matches(File.ReadAllText(DispatcherSource()))
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.IsNotEmpty(dispatched,
            "в диспетчере не нашлось ни одной ветки: сторож ослеп, а не позеленел");

        var declared = McpToolRegistry.Tools.Where(t => !t.StaticText).Select(t => t.Name).ToList();
        var declaredOnly = declared.Except(dispatched).ToList();
        var dispatchedOnly = dispatched.Except(declared).ToList();

        CollectionAssert.AreEquivalent(declared, dispatched,
            "реестр и switch в McpCommandHandler.Handle — один список в двух местах: " +
            "объявленный, но не обработанный инструмент отвечает клиенту «Unknown method», " +
            "а обработанный, но не объявленный не попадёт ни в tools/list сервера, ни в схему " +
            "моста. Инструмент со staticText отвечает мост локально, без вызова Unity, поэтому " +
            "в switch его быть не должно. " +
            $"только в реестре: [{string.Join(", ", declaredOnly)}]; " +
            $"только в диспетчере: [{string.Join(", ", dispatchedOnly)}]");
    }
}
