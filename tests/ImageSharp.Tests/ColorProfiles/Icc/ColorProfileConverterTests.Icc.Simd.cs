// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public partial class ColorProfileConverterTests
{
    /// <summary>
    /// Checks complete profile connections with mixed colors, full SIMD blocks and scalar remainders.
    /// </summary>
    [Fact]
    public void BulkConnections_MatchVerifiedScalarConversions() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckBulkConnections,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the public span contract when the destination contains storage beyond the converted row.
    /// </summary>
    [Fact]
    public void BulkConversion_LeavesExtraDestinationEntriesUntouched()
    {
        ColorProfileConverter converter = new(new ColorConversionOptions
        {
            SourceIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb),
            TargetIccProfile = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2)
        });

        // Cover a full block at each SIMD width and a scalar tail. The extra entry belongs to the caller.
        Rgb[] source = new Rgb[31];
        Array.Fill(source, new Rgb(0.2F, 0.5F, 0.8F));
        Rgb sentinel = new(-1F, -1F, -1F);
        Rgb[] actual = new Rgb[source.Length + 1];
        actual[^1] = sentinel;

        Rgb expected = converter.Convert<Rgb, Rgb>(source[0]);
        converter.Convert<Rgb, Rgb>(source, actual);

        Assert.Equal(sentinel, actual[^1]);
        foreach (Rgb value in actual.AsSpan(0, source.Length))
        {
            VectorAssert.Equal(expected.ToScaledVector4(), value.ToScaledVector4(), 5);
        }
    }

    /// <summary>
    /// Reuses the scalar tests' real profiles and input colors through the public typed converter APIs.
    /// </summary>
    private static void CheckBulkConnections()
    {
        ReadOnlySpan<(string Source, string Target)> connections =
        [
            (TestIccProfiles.StandardRgbV2, TestIccProfiles.RommRgb),
            (TestIccProfiles.RommRgb, TestIccProfiles.StandardRgbV2),
            (TestIccProfiles.Fogra39, TestIccProfiles.StandardRgbV2),
            (TestIccProfiles.StandardRgbV2, TestIccProfiles.Fogra39),
            (TestIccProfiles.JapanColor2011, TestIccProfiles.StandardRgbV4)
        ];

        // Thirty-one mixed colors cover 512-, 256- and 128-bit groups and a scalar tail.
        List<float[]> inputs = Enumerable.Range(0, 31).Select(i => Inputs[i % Inputs.Count]).ToList();
        foreach ((string source, string target) in connections)
        {
            List<Vector4> actual = GetBulkActualTargetValues(inputs, source, target, IccInterpolationMethod.Trilinear);
            for (int i = 0; i < inputs.Count; i++)
            {
                Vector4 expected = GetActualTargetValues(inputs[i], source, target, IccInterpolationMethod.Trilinear);
                VectorAssert.Equal(expected, actual[i], 5);
            }
        }
    }
}
