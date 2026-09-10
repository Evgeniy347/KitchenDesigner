using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Арифметика кадра миниатюры. Дефект, из-за которого эти тесты
/// появились: шар источника света выглядел в каталоге яйцом на боку. Рендер
/// был честным — квадратная RenderTexture 128×128 при aspect = 1, — а сжатие
/// приезжало из плитки: RawImage растягивал квадратную картинку на прямоугольник
/// 96×56, то есть сплющивал её по вертикали в 0,58 раза. Поэтому камера обязана
/// получать aspect РОВНО той цели, в которую рисует, а цель — форму плитки.</summary>
public class ThumbnailFrameTests
{
    [Test]
    public void Aspect_SquareTarget_IsOne()
    {
        Assert.AreEqual(1f, ThumbnailFrame.Aspect(128, 128), 1e-6f,
            "квадратная цель — единичный aspect: иначе круг в кадре станет эллипсом");
    }

    [Test]
    public void Aspect_WideTarget_IsWidthOverHeight()
    {
        Assert.AreEqual(219f / 128f, ThumbnailFrame.Aspect(219, 128), 1e-6f,
            "aspect камеры считается по сторонам самой RenderTexture, а не по экрану");
        Assert.AreEqual(128f / 219f, ThumbnailFrame.Aspect(128, 219), 1e-6f,
            "и противоположный вход даёт противоположный ответ — иначе формула "
            + "молча возвращала бы одно и то же для любой цели");
    }

    [Test]
    public void Aspect_DegenerateTarget_FallsBackToOne()
    {
        Assert.AreEqual(1f, ThumbnailFrame.Aspect(0, 128), 1e-6f,
            "нулевая ширина не имеет права уехать в 0 и обнулить кадр");
        Assert.AreEqual(1f, ThumbnailFrame.Aspect(128, 0), 1e-6f,
            "нулевая высота не имеет права уехать в бесконечность");
    }

    [Test]
    public void SizeForTile_KeepsTheTileProportion_SoTheImageIsNotStretched()
    {
        var size = ThumbnailFrame.SizeForTile(96f, 56f, 128);

        Assert.AreEqual(128, size.y, "высота цели — заказанная");
        Assert.AreEqual(219, size.x,
            "ширина цели выводится из пропорции плитки 96×56, а не берётся квадратной");
        Assert.AreEqual(96f / 56f, ThumbnailFrame.Aspect(size.x, size.y), 96f / 56f * 0.01f,
            "и итоговая пропорция цели совпадает с пропорцией плитки в пределах 1%: "
            + "ровно на эту разницу RawImage растянет картинку, и именно она "
            + "превращала шар в яйцо");
    }

    [Test]
    public void SizeForTile_TallTile_IsTallerThanWide()
    {
        var size = ThumbnailFrame.SizeForTile(56f, 96f, 128);

        Assert.AreEqual(75, size.x,
            "узкая и высокая плитка даёт узкую цель — знак пропорции не потерян");
    }

    [Test]
    public void SizeForTile_DegenerateTile_FallsBackToSquare()
    {
        Assert.AreEqual(new Vector2Int(128, 128), ThumbnailFrame.SizeForTile(0f, 56f, 128),
            "плитка без ширины — не повод отдавать цель нулевой ширины");
        Assert.AreEqual(new Vector2Int(128, 128), ThumbnailFrame.SizeForTile(96f, 0f, 128),
            "плитка без высоты — тем более");
    }

    [Test]
    public void SizeForTile_NeverReturnsZeroPixels()
    {
        var size = ThumbnailFrame.SizeForTile(1f, 1000f, 0);

        Assert.AreEqual(1, size.x, "RenderTexture нулевой ширины Unity не создаст");
        Assert.AreEqual(1, size.y, "и нулевой высоты тоже");
    }
}
