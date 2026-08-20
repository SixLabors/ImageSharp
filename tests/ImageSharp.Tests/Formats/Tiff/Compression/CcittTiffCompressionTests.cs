// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression;

[Trait("Format", "Tiff")]
public class CcittTiffCompressionTests
{
    private const string T4PocGzipBase64 =
        "H4sICBOogmoCA3BvYy10aWZmLXQ0LnRpZgDty7ENwjAQhlFfjKKUoYGSPlPQZopskGVYiJFgA2zpFGUG9Kz3S19xXtelTKX0xaVEq2dbZPcNUY+u2bVtzO7vmvd72+3095792vJyfsQH" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADgH33fP9Zj5xoBYAEA";

    private const string ModifiedHuffmanPocGzipBase64 =
        "H4sICBSogmoCA3BvYy10aWZmLW1oLnRpZgDty7ENgzAQhlEfRBElNKRMnylomSIbZBkWYiTYAFs6RZkhetb7pa84r+urDKW0xa1EraUustu66L/dZ3d19+z2prz/1M0/fx/Z2zsvx2cc" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/+jcL3Buw0IBYAEA";

    private const string TiledT4ReporterPocGzipBase64 =
        "H4sIAAAAAAACCu3UKw7CUBCG0RkuIeC6AxQGj8TwEHWshyWxOhI2QC8Z0QWAIDnNmeQXran4xnEf64jYROQyMvo8RtYepltk++x+rXabblW7P6fZ++fZvtS+T3et/djVR8M2n/Btr1v6C/zCQbQQLUQLRAvRQrRAtBAtRAtEC9FCtEC0EC3+Mlpv/6RrkiQmAAA=";

    [Theory]
    [InlineData(T4PocGzipBase64)]
    [InlineData(ModifiedHuffmanPocGzipBase64)]
    public void Decode_CcittStripWithOversizedRun_ThrowsImageFormatException(string gzipBase64)
    {
        using MemoryStream tiffStream = CreateTiffStream(gzipBase64);

        ImageFormatException exception = Assert.Throws<ImageFormatException>(() => Image.Load(tiffStream));

        Assert.Equal("CCITT compression parsing error: decoded more pixels than the image width.", exception.Message);
    }

    [Fact]
    public void Decode_TiledCcittReporterSample_ThrowsImageFormatException()
    {
        // This is the reporter's exact tiled T4 TIFF sample, gzip-compressed only to keep the test source small.
        using MemoryStream tiffStream = CreateTiffStream(TiledT4ReporterPocGzipBase64);

        ImageFormatException exception = Assert.Throws<ImageFormatException>(() => Image.Load(tiffStream));

        Assert.Equal("CCITT compression parsing error: decoded more pixels than the image width.", exception.Message);
    }

    private static MemoryStream CreateTiffStream(string gzipBase64)
    {
        // Keep the TIFF payloads compressed so the regression source remains small.
        byte[] compressed = Convert.FromBase64String(gzipBase64);
        using MemoryStream compressedStream = new(compressed);
        using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress);
        MemoryStream tiffStream = new();

        gzipStream.CopyTo(tiffStream);
        tiffStream.Position = 0;

        return tiffStream;
    }
}
