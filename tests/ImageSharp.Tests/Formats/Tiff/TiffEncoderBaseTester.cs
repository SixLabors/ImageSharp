// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public abstract class TiffEncoderBaseTester
{
    protected static readonly IImageDecoder ReferenceDecoder = new MagickReferenceDecoder(TiffFormat.Instance);

    protected static void TestStripLength<TPixel>(
        TestImageProvider<TPixel> provider,
        TiffPhotometricInterpretation photometricInterpretation,
        TiffCompression compression,
        bool useExactComparer = true,
        float compareTolerance = 0.01f)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new TiffEncoder() { PhotometricInterpretation = photometricInterpretation, Compression = compression };
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new MemoryStream();
        TiffFrameMetadata inputMeta = input.Frames.RootFrame.Metadata.GetTiffMetadata();
        TiffCompression inputCompression = inputMeta.Compression;

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ExifProfile exifProfileOutput = output.Frames.RootFrame.Metadata.ExifProfile;
        TiffFrameMetadata outputMeta = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        ImageFrame<Rgba32> rootFrame = output.Frames.RootFrame;

        Number rowsPerStrip = exifProfileOutput.GetValue(ExifTag.RowsPerStrip) != null ? exifProfileOutput.GetValue(ExifTag.RowsPerStrip).Value : TiffConstants.RowsPerStripInfinity;
        Assert.True(output.Height > (int)rowsPerStrip);
        Assert.True(exifProfileOutput.GetValue(ExifTag.StripOffsets)?.Value.Length > 1);
        Number[] stripByteCounts = exifProfileOutput.GetValue(ExifTag.StripByteCounts)?.Value;
        Assert.NotNull(stripByteCounts);
        Assert.True(stripByteCounts.Length > 1);

        foreach (Number sz in stripByteCounts)
        {
            Assert.True((uint)sz <= TiffConstants.DefaultStripSize);
        }

        // For uncompressed more accurate test.
        if (compression == TiffCompression.None)
        {
            for (int i = 0; i < stripByteCounts.Length - 1; i++)
            {
                // The difference must be less than one row.
                int stripBytes = (int)stripByteCounts[i];
                int widthBytes = ((int)outputMeta.BitsPerPixel + 7) / 8 * rootFrame.Width;

                Assert.True((TiffConstants.DefaultStripSize - stripBytes) < widthBytes);
            }
        }

        // Compare with reference.
        TestTiffEncoderCore(
           provider,
           inputMeta.BitsPerPixel,
           photometricInterpretation,
           inputCompression,
           useExactComparer: useExactComparer,
           compareTolerance: compareTolerance);
    }

    protected static void TestTiffEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        TiffBitsPerPixel? bitsPerPixel,
        TiffPhotometricInterpretation? photometricInterpretation,
        TiffCompression compression = TiffCompression.None,
        TiffPredictor predictor = TiffPredictor.None,
        bool useExactComparer = true,
        float compareTolerance = 0.001f,
        IImageDecoder imageDecoder = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        TiffEncoder encoder = new TiffEncoder
        {
            PhotometricInterpretation = photometricInterpretation,
            BitsPerPixel = bitsPerPixel,
            Compression = compression,
            HorizontalPredictor = predictor
        };

        // Does DebugSave & load reference CompareToReferenceInput():
        image.VerifyEncoder(
            provider,
            "tiff",
            bitsPerPixel,
            encoder,
            useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
            referenceDecoder: imageDecoder ?? ReferenceDecoder);
    }
}
