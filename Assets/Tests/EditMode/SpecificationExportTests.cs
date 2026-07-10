using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class SpecificationExportTests
{
    private static SpecResult MakeResult(string name)
    {
        var line = new SpecLine
        {
            name = name,
            dimensionsMM = new Vector3Int(800, 400, 18),
            count = 2,
            areaPerBoardM2 = 0.65f,
            totalAreaM2 = 1.30f
        };
        return new SpecResult
        {
            lines = new List<SpecLine> { line },
            totalCount = 2,
            totalAreaM2 = 1.30f
        };
    }

    [Test]
    public void ToCsv_HasHeaderAndTotalRow()
    {
        var csv = SpecificationExport.ToCsv(MakeResult("Board"));
        var lines = csv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        StringAssert.StartsWith("Name;Width_mm;Height_mm;Depth_mm;Count", lines[0]);
        StringAssert.Contains("Board;800;400;18;2", csv);
        StringAssert.StartsWith("Total;;;;2;;", lines[lines.Length - 1]);
    }

    [Test]
    public void ToCsv_EscapesNameContainingSemicolon()
    {
        var csv = SpecificationExport.ToCsv(MakeResult("Left;Right"));
        StringAssert.Contains("\"Left;Right\"", csv);
    }

    [Test]
    public void SaveToFile_WritesCsvFile()
    {
        var path = Path.Combine(Application.temporaryCachePath, "spec_export.csv");
        if (File.Exists(path)) File.Delete(path);

        Assert.IsTrue(SpecificationExport.SaveToFile(MakeResult("Board"), path));
        Assert.IsTrue(File.Exists(path));
        StringAssert.Contains("Board", File.ReadAllText(path));

        File.Delete(path);
    }

    [Test]
    public void SaveToFile_InvalidPath_ReturnsFalse()
    {
        LogAssert.Expect(LogType.Error, new Regex("\\[SpecExport\\] Failed"));
        bool ok = SpecificationExport.SaveToFile(MakeResult("Board"), "Z:\\<>:invalid\\x.csv");
        Assert.IsFalse(ok);
    }
}
