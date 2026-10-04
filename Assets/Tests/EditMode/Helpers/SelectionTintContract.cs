using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Один ответ на вопрос «подкрасило или подменило» для всех, кто его
/// задаёт: чистый тест формулы и обход по всем типам элементов в Unity. Подмена —
/// это когда яркость цвета после выделения ушла дальше середины пути от собственной
/// к жёлтой (светлее собственной яркость быть может — белая доска белее середины).</summary>
public static class SelectionTintContract
{
    public const float HalfWay = 0.5f;
    public const float LumaSlack = 0.01f;

    public static float Ceiling(Color own, bool multi)
    {
        float ownLuma = SelectionTintMath.Luma(own);
        float tintLuma = SelectionTintMath.Luma(SelectionTintMath.TintOf(multi));
        return Mathf.Max(ownLuma, Mathf.Lerp(ownLuma, tintLuma, HalfWay)) + LumaSlack;
    }

    public static bool Replaces(Color own, Color tinted, bool multi) =>
        SelectionTintMath.Luma(tinted) > Ceiling(own, multi);
}
