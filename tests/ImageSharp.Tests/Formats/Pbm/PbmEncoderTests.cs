// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Pbm;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Pbm;

[Collection("RunSerial")]
[Trait("Format", "Pbm")]
public class PbmEncoderTests
{
    public static readonly TheoryData<PbmColorType> ColorType =
        new()
        {
            PbmColorType.BlackAndWhite,
            PbmColorType.Grayscale,
            PbmColorType.Rgb
        };

    public static readonly TheoryData<string, PbmColorType> PbmColorTypeFiles =
        new()
        {
            { BlackAndWhiteBinary, PbmColorType.BlackAndWhite },
            { BlackAndWhitePlain, PbmColorType.BlackAndWhite },
            { Issue2477, PbmColorType.BlackAndWhite },
            { GrayscaleBinary, PbmColorType.Grayscale },
            { GrayscaleBinaryWide, PbmColorType.Grayscale },
            { GrayscalePlain, PbmColorType.Grayscale },
            { RgbBinary, PbmColorType.Rgb },
            { RgbPlain, PbmColorType.Rgb },
        };

    [Theory]
    [MemberData(nameof(PbmColorTypeFiles))]
    public void PbmEncoder_PreserveColorType(string imagePath, PbmColorType pbmColorType)
    {
        PbmEncoder options = new();

        TestFile testFile = TestFile.Create(imagePath);
        using (Image<Rgba32> input = testFile.CreateRgba32Image())
        {
            using (MemoryStream memStream = new())
            {
                input.Save(memStream, options);
                memStream.Position = 0;
                using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                {
                    PbmMetadata meta = output.Metadata.GetPbmMetadata();
                    Assert.Equal(pbmColorType, meta.ColorType);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(PbmColorTypeFiles))]
    public void PbmEncoder_WithPlainEncoding_PreserveBitsPerPixel(string imagePath, PbmColorType pbmColorType)
    {
        PbmEncoder options = new()
        {
            Encoding = PbmEncoding.Plain
        };

        TestFile testFile = TestFile.Create(imagePath);
        using (Image<Rgba32> input = testFile.CreateRgba32Image())
        {
            using (MemoryStream memStream = new())
            {
                input.Save(memStream, options);

                // EOF indicator for plain is a Space.
                memStream.Seek(-1, SeekOrigin.End);
                int lastByte = memStream.ReadByte();
                Assert.Equal(0x20, lastByte);

                memStream.Seek(0, SeekOrigin.Begin);
                using (Image<Rgba32> output = Image.Load<Rgba32>(memStream))
                {
                    PbmMetadata meta = output.Metadata.GetPbmMetadata();
                    Assert.Equal(pbmColorType, meta.ColorType);
                }
            }
        }
    }

    [Theory]
    [WithFile(BlackAndWhitePlain, PixelTypes.Rgb24)]
    public void PbmEncoder_P1_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.BlackAndWhite, PbmEncoding.Plain);

    [Theory]
    [WithFile(BlackAndWhiteBinary, PixelTypes.Rgb24)]
    public void PbmEncoder_P4_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.BlackAndWhite, PbmEncoding.Binary);

    [Theory]
    [WithFile(Issue2477, PixelTypes.Rgb24)]
    public void PbmEncoder_P4_Irregular_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.BlackAndWhite, PbmEncoding.Binary);

    [Theory]
    [WithFile(GrayscalePlainMagick, PixelTypes.Rgb24)]
    public void PbmEncoder_P2_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Grayscale, PbmEncoding.Plain);

    [Theory]
    [WithFile(GrayscaleBinary, PixelTypes.Rgb24)]
    public void PbmEncoder_P5_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Grayscale, PbmEncoding.Binary);

    [Theory]
    [WithFile(RgbPlainMagick, PixelTypes.Rgb24)]
    public void PbmEncoder_P3_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Rgb, PbmEncoding.Plain);

    [Theory]
    [WithFile(RgbBinary, PixelTypes.Rgb24)]
    public void PbmEncoder_P6_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestPbmEncoderCore(provider, PbmColorType.Rgb, PbmEncoding.Binary);

    private static void TestPbmEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        PbmColorType colorType,
        PbmEncoding encoding,
        bool useExactComparer = true,
        float compareTolerance = 0.01f)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            PbmEncoder encoder = new() { ColorType = colorType, Encoding = encoding };

            using (MemoryStream memStream = new())
            {
                image.Save(memStream, encoder);
                memStream.Position = 0;
                using (Image<TPixel> encodedImage = (Image<TPixel>)Image.Load(memStream))
                {
                    ImageComparingUtils.CompareWithReferenceDecoder(provider, encodedImage, useExactComparer, compareTolerance);
                }
            }
        }
    }
}
