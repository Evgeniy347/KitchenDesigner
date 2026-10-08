using System;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>PNG уходит агенту как base64 и нигде на нашей стороне не открывается — значит, единственное,
/// что отличает рисунок от мусора, это тест, который сам разбирает файл по правилам формата.</summary>
public class PlanPngTests
{
    private static PlanCanvas Pattern(int width, int height)
    {
        var canvas = new PlanCanvas(width, height, 0);
        var random = new Random(7);
        for (int i = 0; i < canvas.Pixels.Length; i++) canvas.Pixels[i] = (byte)random.Next(PlanPalette.Count);
        return canvas;
    }

    [Test]
    public void Crc32_OfTheStandardCheckString_IsTheKnownValue()
    {
        var data = Encoding.ASCII.GetBytes("123456789");

        Assert.AreEqual(0xCBF43926u, PlanCrc32.Of(data, 0, data.Length));
    }

    [Test]
    public void Encode_RoundTripsEveryPixelOfAnAsymmetricPicture()
    {
        var canvas = Pattern(97, 53);

        var read = PlanPngReader.Read(PlanPng.Encode(canvas));

        Assert.AreEqual(97, read.Width);
        Assert.AreEqual(53, read.Height);
        CollectionAssert.AreEqual(canvas.Pixels, read.Indices, "ширина 97 и высота 53 различны: перепутанные строки и столбцы не совпали бы");
    }

    [Test]
    public void Encode_WritesAnEightBitIndexedImageWithTheWholePalette()
    {
        var read = PlanPngReader.Read(PlanPng.Encode(Pattern(8, 8)));

        Assert.AreEqual(8, read.BitDepth);
        Assert.AreEqual(3, read.ColourType, "3 = палитровый: один байт на пиксель, плоские цвета сжимаются в разы");
        Assert.AreEqual(PlanPalette.Count * 3, read.Palette.Length);
        for (int i = 0; i < PlanPalette.Count; i++)
            CollectionAssert.AreEqual(PlanPalette.Bytes(i), new[] { read.Palette[i * 3], read.Palette[i * 3 + 1], read.Palette[i * 3 + 2] });
    }

    [Test]
    public void Encode_IsDeterministic()
    {
        var canvas = Pattern(64, 40);

        CollectionAssert.AreEqual(PlanPng.Encode(canvas), PlanPng.Encode(canvas));
    }

    [Test]
    public void Encode_ABlankPictureIsTiny()
    {
        var png = PlanPng.Encode(new PlanCanvas(512, 512, PlanPalette.Background));

        Assert.Less(png.Length, 1000, "262 144 одинаковых пикселей обязаны сжаться почти в ничто");
    }

    [Test]
    public void Encode_TheLastChunk_IsIendWithItsFixedChecksum()
    {
        var png = PlanPng.Encode(Pattern(4, 4));

        var tail = BitConverter.ToString(png, png.Length - 8, 8);
        Assert.AreEqual("49-45-4E-44-AE-42-60-82", tail);
    }
}
