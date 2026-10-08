using System;
using System.IO;
using System.IO.Compression;
using KitchenDesigner.Core.MCP;

/// <summary>Читатель PNG для проверок: разбирает ровно то, что пишет PlanPng, и проверяет всё, что
/// проверил бы настоящий декодер, — подпись, CRC каждого блока, Adler-32 потока, длину строк. Свой читатель
/// нужен, потому что «файл получился» без декодера ничего не говорит о том, что в нём картинка.</summary>
public sealed class PlanPngReader
{
    public int Width;
    public int Height;
    public int BitDepth;
    public int ColourType;
    public byte[] Palette = Array.Empty<byte>();
    public byte[] Indices = Array.Empty<byte>();

    public static PlanPngReader Read(byte[] png)
    {
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (int i = 0; i < signature.Length; i++)
            if (png[i] != signature[i]) throw new InvalidDataException("подпись PNG не совпала");

        var reader = new PlanPngReader();
        var packed = new MemoryStream();
        bool ended = false;
        int at = signature.Length;
        while (at < png.Length)
        {
            int length = Int(png, at);
            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            uint crc = (uint)Int(png, at + 8 + length);
            if (PlanCrc32.Of(png, at + 4, 4 + length) != crc) throw new InvalidDataException("CRC блока " + type);
            reader.Take(type, png, at + 8, length, packed);
            ended |= type == "IEND";
            at += 12 + length;
        }
        if (!ended) throw new InvalidDataException("нет IEND");
        reader.Unpack(packed.ToArray());
        return reader;
    }

    private void Take(string type, byte[] png, int offset, int length, MemoryStream packed)
    {
        if (type == "IHDR")
        {
            Width = Int(png, offset);
            Height = Int(png, offset + 4);
            BitDepth = png[offset + 8];
            ColourType = png[offset + 9];
        }
        else if (type == "PLTE")
        {
            Palette = new byte[length];
            Array.Copy(png, offset, Palette, 0, length);
        }
        else if (type == "IDAT")
        {
            packed.Write(png, offset, length);
        }
    }

    private void Unpack(byte[] zlib)
    {
        if (zlib[0] != 0x78) throw new InvalidDataException("заголовок zlib");
        var raw = new MemoryStream();
        using (var deflate = new DeflateStream(new MemoryStream(zlib, 2, zlib.Length - 6), CompressionMode.Decompress))
            deflate.CopyTo(raw);
        var bytes = raw.ToArray();
        uint expected = (uint)Int(zlib, zlib.Length - 4);
        if (Adler(bytes) != expected) throw new InvalidDataException("Adler-32");
        if (bytes.Length != (Width + 1) * Height) throw new InvalidDataException("длина строк " + bytes.Length);

        Indices = new byte[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            if (bytes[y * (Width + 1)] != 0) throw new InvalidDataException("фильтр строки " + y);
            Array.Copy(bytes, y * (Width + 1) + 1, Indices, y * Width, Width);
        }
    }

    private static uint Adler(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static int Int(byte[] data, int at) =>
        (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
}
