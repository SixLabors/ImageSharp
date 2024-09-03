// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Cicp;

public class CicpProfileTests
{
    [Theory]
    [WithFile(TestImages.Png.AdamHeadsHlg, PixelTypes.Rgba64)]
    public async Task ReadCicpMetadata_FromPng_Works<TPixel>(TestImageProvider<TPixel> provider)
       where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = await provider.GetImageAsync(PngDecoder.Instance);

        CicpProfile actual = image.Metadata.CicpProfile ?? image.Frames.RootFrame.Metadata.CicpProfile;
        CicpProfileContainsExpectedValues(actual);
    }

    [Fact]
    public void WritingPng_PreservesCicpProfile()
    {
        // arrange
        using Image<Rgba32> image = new Image<Rgba32>(1, 1);
        CicpProfile original = CreateCicpProfile();
        image.Metadata.CicpProfile = original;
        PngEncoder encoder = new PngEncoder();

        // act
        using Image<Rgba32> reloadedImage = WriteAndRead(image, encoder);

        // assert
        CicpProfile actual = reloadedImage.Metadata.CicpProfile ?? reloadedImage.Frames.RootFrame.Metadata.CicpProfile;
        CicpProfileIsValidAndEqual(actual, original);
    }

    private static void CicpProfileContainsExpectedValues(CicpProfile cicp)
    {
        Assert.NotNull(cicp);
        Assert.Equal(CicpColorPrimaries.ItuRBt2020_2, cicp.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.AribStdB67, cicp.TransferCharacteristics);
        Assert.Equal(CicpMatrixCoefficients.Identity, cicp.MatrixCoefficients);
        Assert.True(cicp.FullRange);
    }

    private static CicpProfile CreateCicpProfile()
    {
        CicpProfile profile = new CicpProfile()
        {
            ColorPrimaries = CicpColorPrimaries.ItuRBt2020_2,
            TransferCharacteristics = CicpTransferCharacteristics.SmpteSt2084,
            MatrixCoefficients = CicpMatrixCoefficients.Identity,
            FullRange = true,
        };
        return profile;
    }

    private static void CicpProfileIsValidAndEqual(CicpProfile actual, CicpProfile original)
    {
        Assert.NotNull(actual);
        Assert.Equal(actual.ColorPrimaries, original.ColorPrimaries);
        Assert.Equal(actual.TransferCharacteristics, original.TransferCharacteristics);
        Assert.Equal(actual.MatrixCoefficients, original.MatrixCoefficients);
        Assert.Equal(actual.FullRange, original.FullRange);
    }

    private static Image<Rgba32> WriteAndRead(Image<Rgba32> image, IImageEncoder encoder)
    {
        using (MemoryStream memStream = new MemoryStream())
        {
            image.Save(memStream, encoder);
            image.Dispose();

            memStream.Position = 0;
            return Image.Load<Rgba32>(memStream);
        }
    }
}
