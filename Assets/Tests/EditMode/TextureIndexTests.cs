using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Разбор index.json — единственного описания декоров. Чистая функция,
/// поэтому большая часть проверок идёт строкой, без файловой системы. Отдельно
/// проверяется НАСТОЯЩИЙ индекс проекта: опечатка в нём иначе всплыла бы только
/// в собранном приложении пропавшим декором.</summary>
public class TextureIndexTests
{
    private const string Minimal = @"{
        ""version"": 1,
        ""textures"": [
            { ""id"": ""oak"", ""name"": ""Дуб"", ""kind"": ""ЛДСП"", ""file"": ""oak.jpg"",
              ""tileWidthMM"": 800, ""tileHeightMM"": 400, ""color"": ""#C7A873"" }
        ]
    }";

    // --- Разбор ---

    [Test]
    public void Parse_FullEntry_ReadsEveryField()
    {
        var defs = TextureIndex.Parse(Minimal, out var errors);

        CollectionAssert.IsEmpty(errors);
        Assert.AreEqual(1, defs.Count);
        var d = defs[0];
        Assert.AreEqual("oak", d.id);
        Assert.AreEqual("Дуб", d.displayName);
        Assert.AreEqual("ЛДСП", d.kind);
        Assert.AreEqual("oak.jpg", d.fileName);
        Assert.AreEqual(800, d.tileSizeMM);
        Assert.AreEqual(400, d.tileHeightMM);
        Assert.AreEqual(new Color32(0xC7, 0xA8, 0x73, 0xFF), (Color32)d.baseColor);
        Assert.IsTrue(d.HasTextureFile);
    }

    [Test]
    public void Parse_NoFile_IsColorOnlyDecor()
    {
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":""gtv_black"",""name"":""Чёрный"",""kind"":""Металл"",
               ""tileWidthMM"":800,""color"":""#1A1A1C"",""metallic"":0.3,""smoothness"":0.3}]}",
            out var errors);

        CollectionAssert.IsEmpty(errors);
        Assert.IsFalse(defs[0].HasTextureFile);
        Assert.IsNull(defs[0].fileName);
        Assert.AreEqual(0.3f, defs[0].metallic, 0.0001f);
        Assert.AreEqual(0.3f, defs[0].smoothness, 0.0001f);
    }

    [Test]
    public void Parse_MissingOptionalFields_TakeDefaults()
    {
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":""dub_temnyy"",""tileWidthMM"":600}]}", out var errors);

        CollectionAssert.IsEmpty(errors);
        Assert.AreEqual("dub temnyy", defs[0].displayName, "имя выводится из id");
        Assert.AreEqual("ЛДСП", defs[0].kind);
        Assert.AreEqual(0, defs[0].tileHeightMM, "0 = высота по пропорциям картинки");
        Assert.AreEqual(0.2f, defs[0].smoothness, 0.0001f, "пропущенное поле берёт значение по умолчанию");
    }

    // --- Битые записи: пропускается только испорченная, остальные живут ---

    [Test]
    public void Parse_EmptyId_SkipsEntryKeepsRest()
    {
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":"""",""tileWidthMM"":800},{""id"":""ok"",""tileWidthMM"":800}]}",
            out var errors);

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("ok", defs[0].id);
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void Parse_DuplicateId_KeepsFirstOnly()
    {
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":""a"",""name"":""One"",""tileWidthMM"":800},
                             {""id"":""a"",""name"":""Two"",""tileWidthMM"":800}]}",
            out var errors);

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("One", defs[0].displayName);
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void Parse_NonPositiveTileWidth_SkipsEntry()
    {
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":""a"",""tileWidthMM"":0},{""id"":""b"",""tileWidthMM"":800}]}",
            out var errors);

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual("b", defs[0].id);
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void Parse_BadColor_KeepsDecorWithDefaultColor()
    {
        // Декор с неверным тоном лучше пропавшего: id лежит в сохранённых проектах.
        var defs = TextureIndex.Parse(
            @"{""textures"":[{""id"":""a"",""file"":""a.jpg"",""tileWidthMM"":800,""color"":""морковный""}]}",
            out var errors);

        Assert.AreEqual(1, defs.Count);
        Assert.AreEqual(Color.white, defs[0].baseColor, "у декора с картинкой цвет по умолчанию белый");
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void Parse_BrokenJson_ReturnsEmptyWithError()
    {
        var defs = TextureIndex.Parse("{ это не json", out var errors);

        CollectionAssert.IsEmpty(defs);
        Assert.AreEqual(1, errors.Count);
    }

    [Test]
    public void Parse_EmptyOrNull_ReturnsEmptyWithError()
    {
        TextureIndex.Parse(null, out var e1);
        TextureIndex.Parse("   ", out var e2);

        Assert.AreEqual(1, e1.Count);
        Assert.AreEqual(1, e2.Count);
    }

    [Test]
    public void Parse_LeadingBom_StillReads()
    {
        var defs = TextureIndex.Parse("﻿" + Minimal, out var errors);

        CollectionAssert.IsEmpty(errors);
        Assert.AreEqual(1, defs.Count);
    }

    // --- Настоящий индекс проекта ---

    private static string IndexPath =>
        Path.Combine(Application.streamingAssetsPath, TextureLibrary.FolderName,
            TextureLibrary.IndexFileName);

    [Test]
    public void ProjectIndex_ParsesWithoutErrors()
    {
        Assert.IsTrue(File.Exists(IndexPath), $"нет индекса декоров: {IndexPath}");

        var defs = TextureIndex.Parse(File.ReadAllText(IndexPath), out var errors);

        CollectionAssert.IsEmpty(errors, "index.json проекта должен разбираться без замечаний");
        Assert.Greater(defs.Count, 1);
    }

    [Test]
    public void ProjectIndex_EveryEntryHasItsFile()
    {
        var dir = Path.Combine(Application.streamingAssetsPath, TextureLibrary.FolderName);
        var defs = TextureIndex.Parse(File.ReadAllText(IndexPath), out _);

        var missing = new List<string>();
        foreach (var d in defs)
            if (d.HasTextureFile && !File.Exists(Path.Combine(dir, d.fileName!)))
                missing.Add($"{d.id} → {d.fileName}");

        CollectionAssert.IsEmpty(missing, "запись индекса ссылается на несуществующий файл");
    }

    [Test]
    public void ProjectIndex_EveryImageHasItsEntry()
    {
        // Картинка без записи невидима в приложении — её просто нет в списке.
        var dir = Path.Combine(Application.streamingAssetsPath, TextureLibrary.FolderName);
        var defs = TextureIndex.Parse(File.ReadAllText(IndexPath), out _);

        var described = new HashSet<string>();
        foreach (var d in defs)
            if (d.HasTextureFile) described.Add(d.fileName!);

        var orphans = new List<string>();
        foreach (var file in Directory.GetFiles(dir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
            var name = Path.GetFileName(file);
            if (!described.Contains(name)) orphans.Add(name);
        }

        CollectionAssert.IsEmpty(orphans,
            "картинка без записи в index.json не попадёт в список декоров");
    }

    [Test]
    public void ProjectIndex_HasDefaultDecor()
    {
        var defs = TextureIndex.Parse(File.ReadAllText(IndexPath), out _);

        var found = false;
        foreach (var d in defs) if (d.id == MaterialCatalog.DefaultId) found = true;

        Assert.IsTrue(found, $"в индексе обязан быть декор '{MaterialCatalog.DefaultId}'");
    }
}
