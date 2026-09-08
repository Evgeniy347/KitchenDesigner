using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// D1: площадь детали в ведомости раскроя — площадь ПЛАСТИ (два наибольших измерения),
/// не полная площадь поверхности бруска. Кромка уже считается отдельно погонными метрами
/// по `edgeL1/L2/W1/W2`, поэтому торцы не должны попадать в S ещё и через эту колонку —
/// иначе они посчитаны дважды, что и было дефектом (2·(w·h+w·d+h·d) вместо w·h).
/// </summary>
public class BoardFaceAreaTests
{
    [Test]
    public void FaceAreaM2_800x400x18_IsWidthTimesHeightOnly()
    {
        float area = BoardFaceArea.FaceAreaM2(new Vector3Int(800, 400, 18));
        Assert.AreEqual(0.32f, area, 0.0001f,
            "0,8×0,4 — толщина 18 мм выброшена, а не удвоена через торцы");
    }

    [Test]
    public void FaceAreaM2_ThicknessIsAlwaysTheSmallestDimension_NotTheThirdAxis()
    {
        // Толщина стоит на оси Y, а не Z — форма всё равно должна её распознать по значению,
        // а не по позиции координаты.
        float area = BoardFaceArea.FaceAreaM2(new Vector3Int(552, 18, 720));
        Assert.AreEqual(0.552f * 0.720f, area, 0.0001f);
    }

    [Test]
    public void FaceAreaM2_RotatedDimensions_SameAreaRegardlessOfAxisOrder()
    {
        float normal = BoardFaceArea.FaceAreaM2(new Vector3Int(552, 720, 18));
        float rotated = BoardFaceArea.FaceAreaM2(new Vector3Int(18, 720, 552));

        Assert.AreEqual(normal, rotated, 0.0001f,
            "одна и та же деталь, повёрнутая в хранимых размерах, обязана дать одну площадь");
        Assert.AreEqual(0.552f * 0.720f, normal, 0.0001f);
    }

    /// <summary>Кубик — вырожденный случай, где «две наибольшие» не определены однозначно
    /// (все три измерения равны). Решение: площадь пласти = сторона², то есть функция берёт
    /// произведение двух наибольших ЗНАЧЕНИЙ после сортировки, а не двух наибольших осей —
    /// при равенстве всех трёх это автоматически даёт сторона². Закреплено тестом, чтобы
    /// будущая правка не превратила куб в 0 или в утроенную грань.</summary>
    [Test]
    public void FaceAreaM2_Cube_IsSideSquared()
    {
        float area = BoardFaceArea.FaceAreaM2(new Vector3Int(400, 400, 400));
        Assert.AreEqual(0.4f * 0.4f, area, 0.0001f);
    }

    [Test]
    public void FaceAreaM2_TwoDimensionsTieAtTheMinimum_DropsOnlyOneOfThem()
    {
        // 18/18/500: толщина 18 встречается дважды — из площади должен уйти РОВНО один
        // экземпляр минимума, а не оба (иначе площадь стала бы 500×500 вместо 18×500).
        float area = BoardFaceArea.FaceAreaM2(new Vector3Int(18, 18, 500));
        Assert.AreEqual(0.018f * 0.5f, area, 0.0001f);
    }

    [Test]
    public void FaceAreaM2_552x720x18_MatchesDefectReferenceNumber()
    {
        float area = BoardFaceArea.FaceAreaM2(new Vector3Int(552, 720, 18));
        Assert.AreEqual(3.58f / 9f, area, 0.001f, "площадь одной детали из ТЗ (3,58 м² / 9 шт)");
    }
}
