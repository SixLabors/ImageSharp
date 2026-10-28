// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;
using SixLabors.ImageSharp.Tests.TestDataIcc;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc;

[Trait("Profile", "Icc")]
public class IccProfileTests
{
    /// <summary>
    /// Keeps the existing sRGB profile fast paths and recognizes a sampled sRGB profile.
    /// </summary>
    [Fact]
    public void IsCanonicalSrgbMatrixTrc_RecognizesExistingSrgbProfiles()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertCanonicalSrgbProfiles,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the existing sRGB encodings and the two stored JPEG profiles at each supported SIMD width.
    /// </summary>
    private static void AssertCanonicalSrgbProfiles()
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

        // The 1024-point curve fits the one-stored-level bound. Float lookup rounding previously rejected node 957.
        ImageInfo dense = Image.Identify(TestFile.Create(TestImages.Jpeg.Issues.BadSubSampling1076).Bytes);
        Assert.True(dense.Metadata.IccProfile.IsCanonicalSrgbMatrixTrc());

        // The 26-point c2 curve differs measurably from sRGB. Fixing coordinate rounding must not admit this profile.
        ImageInfo sparse = Image.Identify(TestFile.Create(TestImages.Jpeg.Progressive.Fb).Bytes);
        Assert.False(sparse.Metadata.IccProfile.IsCanonicalSrgbMatrixTrc());

        // Change a node outside the five-point fingerprint in each channel of the independent profile.
        // The channel curves still match each other, but the complete curve no longer matches sRGB.
        foreach (IccTagDataEntry entry in sampled.Entries)
        {
            if (entry.TagSignature is IccProfileTag.RedTrc or IccProfileTag.GreenTrc or IccProfileTag.BlueTrc)
            {
                IccCurveTagDataEntry curve = Assert.IsType<IccCurveTagDataEntry>(entry);
                curve.CurveData[curve.CurveData.Length / 3] += 4F / ushort.MaxValue;
            }
        }

        Assert.False(sampled.IsCanonicalSrgbMatrixTrc());
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
    /// Converts linear RGB samples to sRGB in every mode and retains the profile unless removal is requested.
    /// </summary>
    /// <param name="imagePath">The stored reproduction from issue 3216.</param>
    /// <param name="handling">The requested profile handling.</param>
    /// <param name="red">The expected first red sample.</param>
    /// <param name="green">The expected first green sample.</param>
    /// <param name="blue">The expected first blue sample.</param>
    [Theory]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Preserve, 99, 137, 188)]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Compact, 99, 137, 188)]
    [InlineData(TestImages.Png.Icc.LinearRgb, ColorProfileHandling.Convert, 99, 137, 188)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Preserve, 110, 152, 182)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Compact, 110, 152, 182)]
    [InlineData(TestImages.Jpeg.ICC.LinearRgb, ColorProfileHandling.Convert, 110, 152, 182)]
    public void Decode_LinearRgbProfile_UsesRequestedHandling(string imagePath, ColorProfileHandling handling, byte red, byte green, byte blue)
    {
        DecoderOptions options = new(Configuration.Default) { ColorProfileHandling = handling };
        using Image<Rgb24> image = Image.Load<Rgb24>(options, TestFile.Create(imagePath).Bytes);
        Rgb24 actual = image[0, 0];

        // https://github.com/SixLabors/ImageSharp/issues/3216
        // The stored inputs use linear RGB with sRGB primaries. LittleCMS and the sRGB transfer formula
        // give the converted byte values above. Allow one byte level for matrix and destination rounding.
        Assert.InRange(actual.R, red - 1, red + 1);
        Assert.InRange(actual.G, green - 1, green + 1);
        Assert.InRange(actual.B, blue - 1, blue + 1);

        if (handling == ColorProfileHandling.Convert)
        {
            Assert.Null(image.Metadata.IccProfile);
        }
        else
        {
            Assert.NotNull(image.Metadata.IccProfile);
            Assert.False(image.Metadata.IccProfile.IsCanonicalSrgbMatrixTrc());
        }
    }

    /// <summary>
    /// Checks that output selection retains only compatible profiles and configures the matching return transform.
    /// </summary>
    /// <param name="profileFile">The real ICC fixture, or null for unprofiled output.</param>
    /// <param name="colorType">The selected output sample type.</param>
    /// <param name="retainProfile">Whether the original profile can be embedded.</param>
    /// <param name="requiresConversion">Whether samples require conversion from sRGB.</param>
    [Theory]
    [InlineData(null, PixelColorType.RGB, false, false)]
    [InlineData(TestIccProfiles.StandardRgbV2, PixelColorType.RGB, true, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.RGB, true, true)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.BGR | PixelColorType.Alpha, true, true)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Indexed | PixelColorType.Alpha, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.CMYK, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.YCCK, true, true)]
    [InlineData(TestIccProfiles.Fogra39, PixelColorType.RGB, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.CMYK, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Luminance, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.YCbCr, false, false)]
    [InlineData(TestIccProfiles.RommRgb, PixelColorType.Other, false, false)]
    public void GetIccProfileForEncoding_SelectsProfileAndConversion(string profileFile, PixelColorType colorType, bool retainProfile, bool requiresConversion)
    {
        IccProfile profile = profileFile is null ? null : TestIccProfiles.GetProfile(profileFile);
        IccProfile outputProfile = IccProfile.GetIccProfileForEncoding(profile, colorType, MemoryAllocator.Default, out ColorProfileConverter converter);

        // Return the original object, so selection cannot replace metadata with a copy of sRGB or another profile.
        Assert.Same(retainProfile ? profile : null, outputProfile);

        if (requiresConversion)
        {
            Assert.NotNull(converter);
            Assert.Same(CompactSrgbV4Profile.Profile, converter.Options.SourceIccProfile);
            Assert.Same(outputProfile, converter.Options.TargetIccProfile);
            Assert.Same(MemoryAllocator.Default, converter.Options.MemoryAllocator);
        }
        else
        {
            Assert.Null(converter);
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
