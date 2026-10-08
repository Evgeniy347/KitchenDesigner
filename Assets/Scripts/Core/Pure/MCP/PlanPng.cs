using System;
using System.IO;
using System.IO.Compression;

namespace KitchenDesigner.Core.MCP
{
    public static class PlanPng
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private const int BitDepth = 8;
        private const int IndexedColour = 3;
        private const byte ZlibHeaderCmf = 0x78;
        private const byte ZlibHeaderFlg = 0x9C;
        private const uint AdlerModulus = 65521;

        public static byte[] Encode(PlanCanvas canvas)
        {
            using var output = new MemoryStream();
            output.Write(Signature, 0, Signature.Length);
            WriteChunk(output, "IHDR", Header(canvas));
            WriteChunk(output, "PLTE", Palette());
            WriteChunk(output, "IDAT", Compressed(canvas));
            WriteChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static byte[] Header(PlanCanvas canvas)
        {
            var header = new byte[13];
            PutInt(header, 0, canvas.Width);
            PutInt(header, 4, canvas.Height);
            header[8] = BitDepth;
            header[9] = IndexedColour;
            return header;
        }

        private static byte[] Palette()
        {
            var palette = new byte[PlanPalette.Count * 3];
            for (int i = 0; i < PlanPalette.Count; i++) PlanPalette.Bytes(i).CopyTo(palette, i * 3);
            return palette;
        }

        private static byte[] Compressed(PlanCanvas canvas)
        {
            var raw = new byte[(canvas.Width + 1) * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
                Buffer.BlockCopy(canvas.Pixels, y * canvas.Width, raw, y * (canvas.Width + 1) + 1, canvas.Width);

            using var packed = new MemoryStream();
            packed.WriteByte(ZlibHeaderCmf);
            packed.WriteByte(ZlibHeaderFlg);
            using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(raw, 0, raw.Length);
            var adler = Adler32(raw);
            packed.WriteByte((byte)(adler >> 24));
            packed.WriteByte((byte)(adler >> 16));
            packed.WriteByte((byte)(adler >> 8));
            packed.WriteByte((byte)adler);
            return packed.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var value in data)
            {
                a = (a + value) % AdlerModulus;
                b = (b + a) % AdlerModulus;
            }
            return (b << 16) | a;
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var body = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
            data.CopyTo(body, 4);
            var length = new byte[4];
            PutInt(length, 0, data.Length);
            output.Write(length, 0, 4);
            output.Write(body, 0, body.Length);
            var crc = new byte[4];
            PutInt(crc, 0, unchecked((int)PlanCrc32.Of(body, 0, body.Length)));
            output.Write(crc, 0, 4);
        }

        private static void PutInt(byte[] target, int offset, int value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }
    }
}
