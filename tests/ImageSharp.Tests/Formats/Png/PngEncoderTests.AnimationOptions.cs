// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

public partial class PngEncoderTests
{
    /// <summary>
    /// Checks that animation options override conflicting metadata without changing the source.
    /// </summary>
    /// <param name="provider">The existing APNG fixture.</param>
    /// <param name="animateRootFrame">Whether the root frame belongs to the animation.</param>
    /// <param name="colorType">The encoded pixel layout.</param>
    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32, false, PngColorType.RgbWithAlpha)]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32, true, PngColorType.RgbWithAlpha)]
    [WithFile("Png/animated/7-dispose-none.png", PixelTypes.Rgba32, false, PngColorType.Palette)]
    [WithFile("Png/animated/7-dispose-none.png", PixelTypes.Rgba32, true, PngColorType.Palette)]
    public void Encode_AnimationOptions_OverrideConflictingMetadata(
        TestImageProvider<Rgba32> provider, bool animateRootFrame, PngColorType colorType)
    {
        using Image<Rgba32> source = provider.GetImage(PngDecoder.Instance);

        // Distinct values let the encoded result prove which settings took precedence.
        const ushort metadataRepeatCount = 2;
        const ushort encoderRepeatCount = 3;
        PngMetadata metadata = source.Metadata.GetPngMetadata();
        metadata.AnimateRootFrame = !animateRootFrame;
        metadata.RepeatCount = metadataRepeatCount;
        metadata.TransparentColor = Color.Red;

        // The reference uses the same real frames with the requested values supplied through metadata.
        using Image<Rgba32> reference = source.Clone();
        PngMetadata referenceMetadata = reference.Metadata.GetPngMetadata();
        referenceMetadata.AnimateRootFrame = animateRootFrame;
        referenceMetadata.RepeatCount = encoderRepeatCount;
        referenceMetadata.TransparentColor = Color.Blue;
        PngEncoder referenceEncoder = new() { ColorType = colorType };
        PngEncoder encoder = new()
        {
            ColorType = colorType,
            AnimateRootFrame = animateRootFrame,
            RepeatCount = encoderRepeatCount,
            BackgroundColor = Color.Blue
        };

        using MemoryStream expected = new();
        reference.Save(expected, referenceEncoder);
        using MemoryStream encoded = new();
        source.Save(encoded, encoder);
        byte[] file = encoded.ToArray();

        // Encoder options must produce the same file as matching metadata with the options unset.
        Assert.Equal(expected.ToArray(), file);

        // Check the decoded settings and every frame's pixels, not only agreement between the two encodes.
        using Image<Rgba32> actual = Image.Load<Rgba32>(file);
        Assert.Equal(animateRootFrame, actual.Metadata.GetPngMetadata().AnimateRootFrame);
        Assert.Equal((uint)encoderRepeatCount, actual.Metadata.GetPngMetadata().RepeatCount);
        ImageComparer.Exact.VerifySimilarity(source, actual);

        // Overrides apply to the output. The source must retain its conflicting metadata values.
        Assert.Equal(!animateRootFrame, metadata.AnimateRootFrame);
        Assert.Equal((uint)metadataRepeatCount, metadata.RepeatCount);
        Assert.Equal(Color.Red, metadata.TransparentColor);
        source.DebugSave(provider, testOutputDetails: $"{colorType}_{animateRootFrame}", extension: "png", encoder: encoder);
    }
}
