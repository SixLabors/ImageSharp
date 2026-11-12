// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
public class JxlFormatTests
{
    /// <summary>
    /// Verifies the public format identifiers.
    /// </summary>
    [Fact]
    public void FormatProperties_AreAsExpected()
    {
        JxlFormat format = JxlFormat.Instance;

        Assert.Equal("JPEG XL", format.Name);
        Assert.Equal("image/jxl", format.DefaultMimeType);
        Assert.Contains("image/jxl", format.MimeTypes);
        Assert.Contains("jxl", format.FileExtensions);
    }

    /// <summary>
    /// Verifies both signatures, partial headers and headers with trailing bytes.
    /// </summary>
    /// <param name="hex">The header bytes in hexadecimal notation.</param>
    /// <param name="expected">Whether the header identifies JPEG XL.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData("FF", false)]
    [InlineData("FFD8FF", false)]
    [InlineData("FF0A", true)]
    [InlineData("FF0A123456", true)]
    [InlineData("0000000C4A584C200D0A87", false)]
    [InlineData("0000000C4A584C200D0A870A", true)]
    [InlineData("0000000C4A584C200D0A870A0000001466747970", true)]
    [InlineData("0000000C4A584C200D0A870B", false)]
    public void DetectorRecognizesOnlyCompleteSignatures(string hex, bool expected)
    {
        // These are format signatures, not generated image fixtures. No image header or pixel data is constructed.
        byte[] header = Convert.FromHexString(hex);
        JxlImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(header, out IImageFormat format);

        Assert.Equal(expected, detected);
        Assert.Same(expected ? JxlFormat.Instance : null, format);
    }
}
