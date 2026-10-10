using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace LabelService.Rendering.Output;

/// <summary>
/// PNG 编码（T-021）：按画面内容选最小的无损格式，再用 .NET 自带的 zlib（zlib-ng，最快档）压缩，不做行过滤。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>画布底色是不透明的白色，alpha 恒为 255，一律不输出 alpha。</item>
/// <item>每个像素 R = G = B（标签大多是黑白灰）：8 位灰度，数据量是 RGBA 的四分之一；有彩色时：8 位 RGB。</item>
/// <item>黑白输出（mono）：1 位灰度，0 黑 1 白，和 ZPL 同一个阈值（<see cref="Monochrome"/>）。</item>
/// </list>
/// 压缩是单张 PNG 的主要耗时（原来 Skia 编码 RGBA 约 11 ms，占单张总耗时近九成），换成上面的做法后输出和原来逐像素一致（无损）。
/// </remarks>
internal static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// 编码一张 RGBA8888 位图。
    /// </summary>
    /// <param name="bitmap">不透明的 RGBA8888 位图。</param>
    /// <param name="mono">输出 1 位黑白。</param>
    public static byte[] Encode(SKBitmap bitmap, bool mono)
    {
        if (bitmap.ColorType != SKColorType.Rgba8888)
        {
            throw new ArgumentException("只支持 RGBA8888 位图", nameof(bitmap));
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = MemoryMarshal.Cast<byte, uint>(bitmap.GetPixelSpan());
        var stride = bitmap.RowBytes / 4;
        var kind = mono ? Kind.Mono : IsGray(pixels, width, height, stride) ? Kind.Gray : Kind.Rgb;
        var rowBytes = kind switch
        {
            Kind.Mono => (width + 7) / 8,
            Kind.Gray => width,
            _ => width * 3,
        };

        using var output = new MemoryStream(Math.Max(4096, width * height / 16));
        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = kind == Kind.Mono ? (byte)1 : (byte)8;
        header[9] = kind == Kind.Rgb ? (byte)2 : (byte)0;
        WriteChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream(Math.Max(4096, rowBytes * height / 8));
        var row = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        try
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (var y = 0; y < height; y++)
                {
                    var source = pixels.Slice(y * stride, width);
                    var target = row.AsSpan(0, rowBytes + 1);
                    target[0] = 0;
                    switch (kind)
                    {
                        case Kind.Mono:
                            PackMono(source, target[1..]);
                            break;
                        case Kind.Gray:
                            for (var x = 0; x < width; x++)
                            {
                                target[x + 1] = (byte)source[x];
                            }

                            break;
                        default:
                            for (var x = 0; x < width; x++)
                            {
                                var pixel = source[x];
                                target[1 + x * 3] = (byte)pixel;
                                target[2 + x * 3] = (byte)(pixel >> 8);
                                target[3 + x * 3] = (byte)(pixel >> 16);
                            }

                            break;
                    }

                    zlib.Write(target);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(row);
        }

        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    /// <summary>
    /// 是不是每个像素都 R = G = B。RGBA8888 在内存里是 R、G、B、A，按小端读成 uint 后 R 在最低字节。
    /// </summary>
    internal static bool IsGray(ReadOnlySpan<uint> pixels, int width, int height, int stride)
    {
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width);
            var x = 0;
            if (Vector.IsHardwareAccelerated)
            {
                var mask = new Vector<uint>(0xFFFF);
                for (; x + Vector<uint>.Count <= row.Length; x += Vector<uint>.Count)
                {
                    var v = new Vector<uint>(row.Slice(x, Vector<uint>.Count));
                    if (!Vector.EqualsAll((v ^ Vector.ShiftRightLogical(v, 8)) & mask, Vector<uint>.Zero))
                    {
                        return false;
                    }
                }
            }

            for (; x < row.Length; x++)
            {
                var pixel = row[x];
                if (((pixel ^ (pixel >> 8)) & 0xFFFF) != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 一行打包成 1 位：高位在左，1 为白（PNG 灰度的 1 是白，和 ZPL 的 1 为黑相反）。
    /// </summary>
    private static void PackMono(ReadOnlySpan<uint> source, Span<byte> target)
    {
        target.Clear();
        for (var x = 0; x < source.Length; x++)
        {
            var pixel = source[x];
            if (!Monochrome.IsBlack((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16)))
            {
                target[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
        }
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = Crc(Crc(0xFFFFFFFF, type), data) ^ 0xFFFFFFFF;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private enum Kind
    {
        Mono,
        Gray,
        Rgb,
    }
}
