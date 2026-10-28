// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccProfileTests
{
    /// <summary>
    /// Keeps the existing sRGB profile fast paths and recognizes a sampled sRGB profile.
    /// </summary>
    [Fact]
    public void IsCanonicalSrgbMatrixTrc_RecognizesExistingSrgbProfiles()
    {
        Assert.True(TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).IsCanonicalSrgbMatrixTrc());
        Assert.True(TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV4).IsCanonicalSrgbMatrixTrc());
        Assert.True(CompactSrgbV4Profile.Profile.IsCanonicalSrgbMatrixTrc());

        // Clear the ID on an independent copy so this assertion checks the curves and colorants.
        IccProfile sampled = new(TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray());
        sampled.Header.Id = default;
        Assert.True(sampled.IsCanonicalSrgbMatrixTrc());

        using Image<Rgb24> image = Image.Load<Rgb24>(TestFile.Create(TestImages.Jpeg.ICC.SRgb).Bytes);
        Assert.True(image.Metadata.IccProfile.IsCanonicalSrgbMatrixTrc());
    }

    /// <summary>
    /// Rejects identity and gamma-only sampled encodings even when their colorants match sRGB.
    /// </summary>
    /// <param name="sampleCount">Zero for identity, one for gamma, or two for a linear table.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IsCanonicalSrgbMatrixTrc_RejectsLinearCurveEncodings(int sampleCount)
    {
        IccProfile profile = new(TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2).ToByteArray());
        profile.Header.Id = default;
        float[] values = sampleCount switch { 0 => [], 1 => [1F], _ => [0F, 1F] };

        // These are the three ICC curveType representations of a linear response. Replace only
        // the curve metadata on this independent profile; the stored sRGB primaries remain unchanged.
        for (int i = 0; i < profile.Entries.Length; i++)
        {
            IccProfileTag tag = profile.Entries[i].TagSignature;
            if (tag is IccProfileTag.RedTrc or IccProfileTag.GreenTrc or IccProfileTag.BlueTrc)
            {
                profile.Entries[i] = new IccCurveTagDataEntry(values, tag);
            }
        }

        Assert.False(profile.IsCanonicalSrgbMatrixTrc());
    }

    /// <summary>
    /// Converts linear RGB samples only when requested and retains their profile in the other modes.
    /// </summary>
    /// <param name="imagePath">The stored reproduction from issue 3216.</param>
    /// <param name="handling">The requested profile handling.</param>
    /// <param name="red">The expected first red sample.</param>
    /// <param name="green">The expected first green sample.</param>
    /// <param name="blue">The expected first blue sample.</param>
    [Theory]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Preserve, 32, 64, 128)]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Compact, 32, 64, 128)]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Convert, 99, 137, 188)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Preserve, 40, 80, 120)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Compact, 40, 80, 120)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Convert, 110, 152, 182)]
    public void Decode_LinearRgbProfile_UsesRequestedHandling(string imagePath, ColorProfileHandling handling, byte red, byte green, byte blue)
    {
        DecoderOptions options = new() { ColorProfileHandling = handling };
        using Image<Rgb24> image = Image.Load<Rgb24>(options, TestFile.Create(imagePath).Bytes);
        Rgb24 actual = image[0, 0];

        // https://github.com/SixLabors/ImageSharp/issues/3216
        // The stored inputs use linear RGB with sRGB primaries. LittleCMS and the sRGB transfer formula
        // give the converted byte values above. Allow one byte level for matrix and destination rounding.
        if (handling == ColorProfileHandling.Convert)
        {
            Assert.InRange(actual.R, red - 1, red + 1);
            Assert.InRange(actual.G, green - 1, green + 1);
            Assert.InRange(actual.B, blue - 1, blue + 1);
            Assert.Null(image.Metadata.IccProfile);
        }
        else
        {
            Assert.Equal(new Rgb24(red, green, blue), actual);
            Assert.NotNull(image.Metadata.IccProfile);
            Assert.False(image.Metadata.IccProfile.IsCanonicalSrgbMatrixTrc());
        }
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileIdTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CalculateHash_WithByteArray_CalculatesProfileHash(byte[] data, IccProfileId expected)
    {
        IccProfileId result = IccProfile.CalculateHash(data);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateHash_WithByteArray_DoesNotModifyData()
    {
        byte[] data = IccTestDataProfiles.ProfileRandomArray;
        byte[] copy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, copy, 0, data.Length);

        IccProfile.CalculateHash(data);

        Assert.Equal(data, copy);
    }

    [Theory]
    [MemberData(nameof(IccTestDataProfiles.ProfileValidityTestData), MemberType = typeof(IccTestDataProfiles))]
    public void CheckIsValid_WithProfiles_ReturnsValidity(byte[] data, bool expected)
    {
        IccProfile profile = new(data);

        bool result = profile.CheckIsValid();

        Assert.Equal(expected, result);
    }
}
