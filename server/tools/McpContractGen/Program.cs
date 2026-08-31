using McpContractGen;

var outPath = args.Length > 0 ? args[0] : Path.Combine("src", "tools.generated.ts");
var text = McpContractEmitter.Emit();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, text);
Console.WriteLine($"[McpContractGen] wrote {McpContractEmitter.ToolCount} tools -> {Path.GetFullPath(outPath)}");
