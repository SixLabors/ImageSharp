// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

public partial class AniEncoderTests
{
    /// <summary>
    /// Checks resource profiles and pixels against direct PNG/BMP encoding, including cropped resources and SkipMetadata.
    /// </summary>
    /// <param name="provider">The existing RGB fixture.</param>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="compression">The codec used for each icon resource.</param>
    [Theory]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgb24, AniFrameFormat.Ico, IconFrameCompression.Png)]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgb24, AniFrameFormat.Ico, IconFrameCompression.Bmp)]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgb24, AniFrameFormat.Cur, IconFrameCompression.Png)]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgb24, AniFrameFormat.Cur, IconFrameCompression.Bmp)]
    [WithFile(TestImages.Bmp.Bit32Rgba, PixelTypes.Rgb24, AniFrameFormat.Bmp, IconFrameCompression.Bmp)]
    public void Encode_IccProfile_PreservesResourceProfiles(TestImageProvider<Rgb24> provider, AniFrameFormat format, IconFrameCompression compression)
    {
        using Image<Rgb24> source = provider.GetImage();
        source.Frames.AddFrame(source.Frames.RootFrame);
        source.Metadata.GetAniMetadata().BitCount = 32;
        source.Metadata.GetAniMetadata().Planes = 1;

        // The first resource overrides the image profile. The next resource must use the image fallback, not the first profile.
        IccProfile frameProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb);
        IccProfile imageProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        source.Metadata.IccProfile = imageProfile;
        source.Frames[0].Metadata.IccProfile = frameProfile;
        source.Frames[1].Metadata.IccProfile = null;
        using Image<Rgb24> unchanged = source.Clone();

        foreach (bool skipMetadata in new[] { false, true })
        {
            // Raw ANI bitmaps use the full canvas. Icon resources also exercise the top-left crop used for smaller variants.
            int[] widths = format == AniFrameFormat.Bmp ? [source.Width] : [source.Width, source.Width / 2];

            foreach (int width in widths)
            {
                for (int i = 0; i < source.Frames.Count; i++)
                {
                    AniFrameMetadata metadata = source.Frames[i].Metadata.GetAniMetadata();
                    metadata.FrameFormat = format;
                    metadata.Compression = compression;
                    metadata.EncodingWidth = (byte)width;
                    metadata.EncodingHeight = (byte)source.Height;
                    metadata.SequenceNumber = i + 1;
                }

                IImageEncoder encoder = new AniEncoder { SkipMetadata = skipMetadata };
                using MemoryStream encoded = new();
                source.Save(encoded, encoder);

                using Image<Rgb24> actual = Image.Load<Rgb24>(encoded.ToArray());

                Assert.Equal(source.Frames.Count, actual.Frames.Count);

                for (int i = 0; i < source.Frames.Count; i++)
                {
                    IccProfile expectedProfile = i == 0 ? frameProfile : imageProfile;

                    Assert.Equal(expectedProfile.ToByteArray(), Assert.IsType<IccProfile>(actual.Frames[i].Metadata.IccProfile).ToByteArray());

                    // Encode the same real pixels directly. Container output must match the owning codec's conversion and byte packing.
                    using Image<Rgb24> reference = source.Frames.CloneFrame(i);
                    reference.Mutate(x => x.Crop(new Rectangle(0, 0, width, source.Height)));
                    reference.Frames.RootFrame.Metadata.IccProfile = expectedProfile;

                    IImageEncoder resourceEncoder = compression == IconFrameCompression.Png
                        ? new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8, SkipMetadata = skipMetadata }
                        : new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Bit32, SkipMetadata = skipMetadata };

                    using MemoryStream resource = new();
                    reference.Save(resource, resourceEncoder);

                    using Image<Rgb24> expected = Image.Load<Rgb24>(resource.ToArray());
                    using Image<Rgb24> actualFrame = actual.Frames.CloneFrame(i);

                    ImageComparer.Exact.VerifySimilarity(expected, actualFrame);
                }

                source.DebugSave(provider, testOutputDetails: $"{format}_{compression}_{width}_{skipMetadata}", extension: "ani", encoder: encoder);
            }
        }

        // Encoding must leave the original pixels and both profile sources unchanged.
        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(imageProfile, source.Metadata.IccProfile);
        Assert.Same(frameProfile, source.Frames[0].Metadata.IccProfile);
        Assert.Null(source.Frames[1].Metadata.IccProfile);
    }
}
