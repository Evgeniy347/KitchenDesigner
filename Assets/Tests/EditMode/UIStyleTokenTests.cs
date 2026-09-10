using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class UIStyleTokenTests
{
    private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    private static IEnumerable<(string name, string glyph)> Glyphs() =>
        typeof(UIStyle).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.Name.StartsWith("Glyph"))
            .Select(f => (f.Name, (string)f.GetValue(null)!));

    [Test]
    public void UIStyle_EveryGlyph_LivesInWgl4()
    {
        Assume.That(Glyphs().Count(), Is.GreaterThan(3), "скан не нашёл глифов — он бы зеленел впустую");

        var outside = Glyphs()
            .SelectMany(g => g.glyph.Select(ch => (g.name, ch)))
            .Where(p => !Wgl4CharSet.Contains(p.ch))
            .Select(p => p.name + " = U+" + ((int)p.ch).ToString("X4"))
            .ToList();

        Assert.IsEmpty(outside,
            "Рантайм-атлас TMP собирается из LiberationSans по WGL4, и глиф вне этого набора "
            + "(✓ U+2713, ✕ U+2715, ▸ U+25B8, ∠ U+2220) в атласе просто отсутствует — в кнопке "
            + "вместо него пустое место. Вне WGL4: " + string.Join(", ", outside));
    }

    [Test]
    public void UIStyle_GlyphAngle_IsTheRightAngleSign_NotTheAngleSign()
    {
        Assert.AreEqual("∟", UIStyle.GlyphAngle,
            "Замер не по оси помечается «∟» (U+221F, знак прямого угла) именно потому, что "
            + "похожий знак угла «∠» (U+2220) в WGL4 не входит");
        Assert.IsFalse(Wgl4CharSet.Contains('∠'), "∠ вне WGL4 — на этом и держится выбор");
    }

    [Test]
    public void UIStyle_GlyphConfirm_IsPureAscii()
    {
        Assert.IsTrue(UIStyle.GlyphConfirm.All(ch => ch < 128),
            "Взведённое удаление ставится вместо «×» в узкой кнопке строки списка и обязано "
            + "быть в атласе при ЛЮБОМ шрифте, поэтому только ASCII: " + UIStyle.GlyphConfirm);
    }

    [Test]
    public void UIStyle_InactiveTab_IsDarkerThanTheActiveOne()
    {
        Assert.Less(Luma(UIStyle.SurfaceInactive), Luma(UIStyle.Surface),
            "Невыбранная вкладка обязана быть темнее обычной поверхности");
        Assert.Less(Luma(UIStyle.Surface), Luma(UIStyle.SurfaceActive),
            "Активная вкладка светлее прочих — только так она читается как приподнятая");
    }

    [Test]
    public void UIStyle_EdgeHighlight3D_StaysTranslucent_AndIsRed()
    {
        Assert.Less(UIStyle.EdgeHighlight3D.a, 1f,
            "Подсветка стороны — накладка на самой детали: под ней должна угадываться деталь");
        Assert.Greater(UIStyle.EdgeHighlight3D.a, 0.5f, "иначе накладку не видно вовсе");
        Assert.Greater(UIStyle.EdgeHighlight3D.r, UIStyle.EdgeHighlight3D.g + 0.5f,
            "Ярко-красная, а не жёлтая: на светлой текстуре детали жёлтая заливка не читалась");
        Assert.Greater(UIStyle.EdgeHighlight3D.r, UIStyle.EdgeHighlight3D.b + 0.5f,
            "Ярко-красная, а не жёлтая: на светлой текстуре детали жёлтая заливка не читалась");
    }

    [Test]
    public void UIStyle_PreviewGhost_IsGreen_AndSeeThrough()
    {
        Assert.Less(UIStyle.PreviewGhost.a, 0.7f,
            "Призрак кандидата — предложение, а не факт: сквозь него обязано быть видно "
            + "место, куда он встанет, иначе превью неотличимо от применённой правки");
        Assert.Greater(UIStyle.PreviewGhost.a, 0.2f, "иначе призрака не видно вовсе");
        Assert.Greater(UIStyle.PreviewGhost.g, UIStyle.PreviewGhost.r + 0.3f,
            "Зелёный отвечает на вопрос «что будет», красный (EdgeHighlight3D) — на вопрос "
            + "«где это»; сблизь их оттенки, и превью прочитается как ошибка");
        Assert.Greater(UIStyle.PreviewGhost.g, UIStyle.PreviewGhost.b + 0.3f,
            "не голубой: голубым красится призрак ПЕРЕТАСКИВАНИЯ, а это другой смысл");
    }
}
