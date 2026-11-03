// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <content>
/// Verifies HDR transfer curves and display-luminance adjustment against original libjxl numeric outputs.
/// </content>
public partial class CompandingTests
{
    // Original libjxl v0.13.0, revision 91837342b24034078780b687f57f670a7def8153:
    // TF_PQ_Base and TF_HLG_Base supply independent component equations. Decoder GetToLinearStage/GetFromLinearStage
    // supply RGB HLG expectations, including primary-weighted luminance and display intensity. These are fixed numeric samples.
    private static readonly Vector3[] NativeHdrInputs =
    [
        new(-0F, 0F, 9.999999747e-05F),
        new(-0.25F, 0.1000000015F, 0.5F),
        new(1F, 1.100000024F, 1.25F),
        new(0.01999999955F, 0.400000006F, 0.8000000119F),
        new(-0.05000000075F, 0.3000000119F, 1.200000048F),
    ];

    private static readonly Vector3[][] NativePqExpanded =
    [
        [
            new(0F, 0F, 2.798862697e-09F),
            new(-0.02021245472F, 0.001272806316F, 0.3617478907F),
            new(39.2156868F, 104.1421967F, 504.5805664F),
            new(3.400606874e-05F, 0.1272467524F, 6.098739624F),
            new(-0.0002353005402F, 0.03936560079F, 292.5645752F),
        ],
        [
            new(0F, 0F, 7.1370998e-10F),
            new(-0.00515417615F, 0.0003245655971F, 0.09224570543F),
            new(10F, 26.55625916F, 128.668045F),
            new(8.671547221e-06F, 0.03244791925F, 1.555178523F),
            new(-6.000163921e-05F, 0.01003822777F, 74.60396576F),
        ],
        [
            new(0F, 0F, 7.137099939e-11F),
            new(-0.0005154176033F, 3.245655898e-05F, 0.009224571288F),
            new(1F, 2.65562582F, 12.86680412F),
            new(8.671547107e-07F, 0.003244792111F, 0.1555178463F),
            new(-6.000163921e-06F, 0.001003822777F, 7.46039629F),
        ],
    ];

    private static readonly Vector3[][] NativePqCompressed =
    [
        [
            new(0F, 0F, 0.0338553898F),
            new(-0.4636211991F, 0.3783048391F, 0.5326556563F),
            new(0.6046490669F, 0.6147310734F, 0.628311038F),
            new(0.2492498606F, 0.5100672245F, 0.5812025666F),
            new(-0.3191877306F, 0.4814449251F, 0.6239675879F),
        ],
        [
            new(0F, 0F, 0.06233686209F),
            new(-0.6025591493F, 0.5080783963F, 0.6765847802F),
            new(0.7518270612F, 0.7622197866F, 0.7761653662F),
            new(0.3570123911F, 0.6525785923F, 0.7275251746F),
            new(-0.44028157F, 0.6218628287F, 0.7717114687F),
        ],
        [
            new(0F, 0F, 0.1499457359F),
            new(-0.8517026305F, 0.7518271208F, 0.9265466928F),
            new(1F, 1.009956598F, 1.023248911F),
            new(0.5791332722F, 0.9025723934F, 0.9765438437F),
            new(-0.6765848398F, 0.8714864254F, 1.019012094F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgExpandedRgb =
    [
        [
            new(-0F, 0F, 5.783149071e-09F),
            new(-0.02390510403F, 0.003824817017F, 0.0956204161F),
            new(0.9863445163F, 1.707822323F, 3.920354843F),
            new(0.0001428307005F, 0.05713228881F, 0.3672004044F),
            new(-0.0008635699633F, 0.03108851798F, 3.120228052F),
        ],
        [
            new(-0F, 0F, 3.973391061e-11F),
            new(-0.006895240396F, 0.001103238552F, 0.02758096159F),
            new(1.116877913F, 1.933836579F, 4.439177036F),
            new(7.66828307e-05F, 0.03067313507F, 0.1971422434F),
            new(-0.0006257182686F, 0.02252585627F, 2.260828495F),
        ],
        [
            new(-0F, 0F, 5.855971281e-16F),
            new(-0.0004290099023F, 6.864158786e-05F, 0.001716039609F),
            new(1.474265814F, 2.55264163F, 5.859661579F),
            new(1.91124218e-05F, 0.007644969504F, 0.04913571477F),
            new(-0.0003046761558F, 0.01096834149F, 1.10084784F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgCompressedRgb =
    [
        [
            new(-0F, 0F, 0.01489278302F),
            new(-0.7238216996F, 0.5264741778F, 0.857678473F),
            new(1.000400662F, 1.017839432F, 1.041171312F),
            new(0.2416737229F, 0.8243744969F, 0.9540410042F),
            new(-0.3812403381F, 0.7681210041F, 1.027573824F),
        ],
        [
            new(-0F, 0F, 0.04645323381F),
            new(-0.8323501348F, 0.6510172486F, 0.961771369F),
            new(0.9973803163F, 1.014825821F, 1.038165927F),
            new(0.2674655318F, 0.8627598882F, 0.9913372993F),
            new(-0.4293136597F, 0.813924551F, 1.070834517F),
        ],
        [
            new(-0F, 0F, 0.1991378814F),
            new(-0.9659041762F, 0.7939747572F, 1.0924716F),
            new(0.993514061F, 1.010968208F, 1.034318924F),
            new(0.3045202494F, 0.9112929106F, 1.038783193F),
            new(-0.499766916F, 0.8714680672F, 1.125948071F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgRec2020ExpandedRgb =
    [
        [
            new(-0F, 0F, 5.811532144e-09F),
            new(-0.02440484427F, 0.003904775251F, 0.09761937708F),
            new(0.9872887731F, 1.709457278F, 3.924108028F),
            new(0.0001432122663F, 0.05728491396F, 0.3681813478F),
            new(-0.000867519062F, 0.03123068623F, 3.134496927F),
        ],
        [
            new(-0F, 0F, 3.820037342e-11F),
            new(-0.005838641431F, 0.0009341826662F, 0.02335456572F),
            new(1.108318686F, 1.91901648F, 4.405157089F),
            new(7.505560643e-05F, 0.0300222449F, 0.1929588467F),
            new(-0.0006031820667F, 0.0217145551F, 2.179401398F),
        ],
        [
            new(-0F, 0F, 5.10003355e-16F),
            new(-0.0002392210299F, 3.827536784e-05F, 0.0009568841197F),
            new(1.434972167F, 2.484606028F, 5.703483582F),
            new(1.772578617e-05F, 0.007090314757F, 0.04557084292F),
            new(-0.0002678536985F, 0.009642733261F, 0.9678018689F),
        ],
    ];

    private static readonly Vector3[][] NativeHlgRec2020CompressedRgb =
    [
        [
            new(-0F, 0F, 0.01485544164F),
            new(-0.7210807204F, 0.5231634378F, 0.8550863266F),
            new(1.000370741F, 1.01780951F, 1.041141629F),
            new(0.2414517254F, 0.8240243793F, 0.9537019134F),
            new(-0.3807266653F, 0.7675958276F, 1.027081609F),
        ],
        [
            new(-0F, 0F, 0.04722134769F),
            new(-0.8493649364F, 0.6696894765F, 0.978296876F),
            new(0.9975756407F, 1.015020609F, 1.038360238F),
            new(0.2690761983F, 0.865018785F, 0.9935390949F),
            new(-0.4331116676F, 0.817289412F, 1.074035883F),
        ],
        [
            new(-0F, 0F, 0.2073885351F),
            new(-1.006713271F, 0.8364598155F, 1.132751226F),
            new(0.9939977527F, 1.011450768F, 1.034800172F),
            new(0.3090798259F, 0.9168172479F, 1.044201016F),
            new(-0.5105798244F, 0.8796463609F, 1.133838058F),
        ],
    ];

    private static readonly Vector3[] NativeHlgExpanded =
    [
        new(0F, 0F, 3.333333165e-09F),
        new(-0.02083333395F, 0.003333333414F, 0.08333333582F),
        new(1F, 1.731466889F, 3.974631786F),
        new(0.0001333333203F, 0.05333333462F, 0.3427838981F),
        new(-0.0008333333535F, 0.03000000305F, 3.01097846F),
    ];

    private static readonly Vector3[] NativeHlgCompressed =
    [
        new(0F, 0F, 0.01732050814F),
        new(-0.7385492921F, 0.5440894961F, 0.8716434836F),
        new(1F, 1.017439127F, 1.040772319F),
        new(0.2449489683F, 0.8294972777F, 0.9590049386F),
        new(-0.3872983456F, 0.7742521167F, 1.033327818F),
    ];

    /// <summary>
    /// Checks scalar colors, packed triples and complete SIMD groups with narrower and scalar tails against native component equations.
    /// </summary>
    [Fact]
    public void HdrComponentCompandingMatchesNative() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckHdrComponentCompanding,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks HLG RGB luminance adjustment with sRGB and Rec.2020 primaries at three display peaks.
    /// </summary>
    [Fact]
    public void HlgWorkingSpaceMatchesNative() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckHlgWorkingSpace,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the component APIs against original native equations without fitting native SIMD approximations outside their unit domain.
    /// </summary>
    private static void CheckHdrComponentCompanding()
    {
        ReadOnlySpan<float> peaks = [255F, 1000F, 10000F];
        ReadOnlySpan<int> lengths = [0, 1, 2, 3, 17, 33];
        Vector3 sentinel = new(-123F, 456F, -789F);

        for (int curve = 0; curve < 2; curve++)
        {
            int peakCount = curve == 0 ? peaks.Length : 1;
            for (int peakIndex = 0; peakIndex < peakCount; peakIndex++)
            {
                for (int direction = 0; direction < 2; direction++)
                {
                    bool compress = direction != 0;
                    Vector3[] expected = curve == 0
                        ? (compress ? NativePqCompressed[peakIndex] : NativePqExpanded[peakIndex])
                        : (compress ? NativeHlgCompressed : NativeHlgExpanded);

                    foreach (int length in lengths)
                    {
                        Vector3[] packed = new Vector3[length + 1];
                        Vector4[] rgba = new Vector4[length + 1];
                        for (int i = 0; i < length; i++)
                        {
                            Vector3 input = NativeHdrInputs[i % NativeHdrInputs.Length];
                            packed[i] = input;
                            rgba[i] = new Vector4(input, HdrAlpha(i));
                            Vector4 scalar = ApplyHdrCurve(curve, compress, rgba[i], peaks[peakIndex]);
                            AssertNativeHdrRgb(expected[i % expected.Length], scalar,
                                $"curve={curve}, compress={compress}, peak={peaks[peakIndex]}, input={input}, scalar");
                            Assert.Equal(BitConverter.SingleToInt32Bits(rgba[i].W), BitConverter.SingleToInt32Bits(scalar.W));
                        }

                        packed[^1] = sentinel;
                        rgba[^1] = new Vector4(sentinel, -17F);
                        ApplyHdrCurve(curve, compress, packed.AsSpan(0, length), peaks[peakIndex]);
                        ApplyHdrCurve(curve, compress, rgba.AsSpan(0, length), peaks[peakIndex]);

                        for (int i = 0; i < length; i++)
                        {
                            Vector3 reference = expected[i % expected.Length];
                            AssertNativeHdrRgb(reference, new Vector4(packed[i], 0F),
                                $"curve={curve}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHdrInputs[i % NativeHdrInputs.Length]}, RGB");
                            AssertNativeHdrRgb(reference, rgba[i],
                                $"curve={curve}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHdrInputs[i % NativeHdrInputs.Length]}, RGBA");
                            Assert.Equal(BitConverter.SingleToInt32Bits(HdrAlpha(i)), BitConverter.SingleToInt32Bits(rgba[i].W));
                        }

                        Assert.Equal(sentinel, packed[^1]);
                        Assert.Equal(new Vector4(sentinel, -17F), rgba[^1]);
                    }

                    // The public signed extension retains both zero signs. Native scalar helpers return positive zero for both inputs.
                    Vector4 zero = ApplyHdrCurve(curve, compress, new Vector4(-0F, 0F, -0F, .375F), peaks[peakIndex]);
                    Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(zero.X));
                    Assert.Equal(0, BitConverter.SingleToInt32Bits(zero.Y));
                    Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(zero.Z));
                }
            }
        }
    }

    /// <summary>
    /// Checks prepared HLG working spaces through all public color layouts with native RGB expectations.
    /// </summary>
    private static void CheckHlgWorkingSpace()
    {
        ReadOnlySpan<float> peaks = [255F, 1000F, 10000F];
        Vector3 sentinel = new(-123F, 456F, -789F);

        for (int space = 0; space < 2; space++)
        {
            RgbWorkingSpace primaries = space == 0 ? KnownRgbWorkingSpaces.SRgb : KnownRgbWorkingSpaces.Rec2020;
            for (int peakIndex = 0; peakIndex < peaks.Length; peakIndex++)
            {
                HlgWorkingSpace workingSpace = new(peaks[peakIndex], primaries.WhitePoint, primaries.ChromaticityCoordinates);
                for (int direction = 0; direction < 2; direction++)
                {
                    bool compress = direction != 0;
                    Vector3[] expected = space == 0
                        ? (compress ? NativeHlgCompressedRgb[peakIndex] : NativeHlgExpandedRgb[peakIndex])
                        : (compress ? NativeHlgRec2020CompressedRgb[peakIndex] : NativeHlgRec2020ExpandedRgb[peakIndex]);

                    // Thirty-three colors cover complete 512-, 256- and 128-bit groups and the final scalar color.
                    Vector3[] packed = new Vector3[34];
                    Vector4[] rgba = new Vector4[34];
                    for (int i = 0; i < 33; i++)
                    {
                        Vector3 input = NativeHdrInputs[i % NativeHdrInputs.Length];
                        packed[i] = input;
                        rgba[i] = new Vector4(input, HdrAlpha(i));
                        Vector4 scalar = compress ? workingSpace.Compress(rgba[i]) : workingSpace.Expand(rgba[i]);
                        AssertNativeHdrRgb(expected[i % expected.Length], scalar,
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={input}, scalar");
                        Assert.Equal(BitConverter.SingleToInt32Bits(rgba[i].W), BitConverter.SingleToInt32Bits(scalar.W));
                    }

                    packed[^1] = sentinel;
                    rgba[^1] = new Vector4(sentinel, -17F);
                    if (compress)
                    {
                        workingSpace.Compress(packed.AsSpan(0, 33));
                        workingSpace.Compress(rgba.AsSpan(0, 33));
                    }
                    else
                    {
                        workingSpace.Expand(packed.AsSpan(0, 33));
                        workingSpace.Expand(rgba.AsSpan(0, 33));
                    }

                    for (int i = 0; i < 33; i++)
                    {
                        Vector3 reference = expected[i % expected.Length];
                        AssertNativeHdrRgb(reference, new Vector4(packed[i], 0F),
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHdrInputs[i % NativeHdrInputs.Length]}, RGB");
                        AssertNativeHdrRgb(reference, rgba[i],
                            $"HLG space={space}, compress={compress}, peak={peaks[peakIndex]}, input={NativeHdrInputs[i % NativeHdrInputs.Length]}, RGBA");
                        Assert.Equal(BitConverter.SingleToInt32Bits(HdrAlpha(i)), BitConverter.SingleToInt32Bits(rgba[i].W));
                    }

                    Assert.Equal(sentinel, packed[^1]);
                    Assert.Equal(new Vector4(sentinel, -17F), rgba[^1]);
                }
            }
        }
    }

    /// <summary>
    /// Applies one public HDR component curve to a color.
    /// </summary>
    /// <param name="curve">Zero selects PQ; one selects HLG.</param>
    /// <param name="compress">Whether to encode linear input.</param>
    /// <param name="value">The input color.</param>
    /// <param name="peak">The linear PQ normalization luminance.</param>
    /// <returns>The converted color with unchanged alpha.</returns>
    private static Vector4 ApplyHdrCurve(int curve, bool compress, Vector4 value, float peak)
        => curve == 0
            ? (compress ? PqCompanding.Compress(value, peak) : PqCompanding.Expand(value, peak))
            : (compress ? HlgCompanding.Compress(value) : HlgCompanding.Expand(value));

    /// <summary>
    /// Applies one public HDR component curve to packed RGB colors.
    /// </summary>
    /// <param name="curve">Zero selects PQ; one selects HLG.</param>
    /// <param name="compress">Whether to encode linear input.</param>
    /// <param name="values">The input and output colors.</param>
    /// <param name="peak">The linear PQ normalization luminance.</param>
    private static void ApplyHdrCurve(int curve, bool compress, Span<Vector3> values, float peak)
    {
        if (curve == 0)
        {
            if (compress)
            {
                PqCompanding.Compress(values, peak);
            }
            else
            {
                PqCompanding.Expand(values, peak);
            }
        }
        else if (compress)
        {
            HlgCompanding.Compress(values);
        }
        else
        {
            HlgCompanding.Expand(values);
        }
    }

    /// <summary>
    /// Applies one public HDR component curve to RGB colors with alpha.
    /// </summary>
    /// <param name="curve">Zero selects PQ; one selects HLG.</param>
    /// <param name="compress">Whether to encode linear input.</param>
    /// <param name="values">The input and output colors.</param>
    /// <param name="peak">The linear PQ normalization luminance.</param>
    private static void ApplyHdrCurve(int curve, bool compress, Span<Vector4> values, float peak)
    {
        if (curve == 0)
        {
            if (compress)
            {
                PqCompanding.Compress(values, peak);
            }
            else
            {
                PqCompanding.Expand(values, peak);
            }
        }
        else if (compress)
        {
            HlgCompanding.Compress(values);
        }
        else
        {
            HlgCompanding.Expand(values);
        }
    }

    /// <summary>
    /// Compares native RGB values with the existing power-approximation bound in normalized component units.
    /// </summary>
    /// <param name="expected">The original native result.</param>
    /// <param name="actual">The converted RGB color. Alpha is checked separately.</param>
    /// <param name="context">The curve, peak, input and public API layout.</param>
    private static void AssertNativeHdrRgb(Vector3 expected, Vector4 actual, string context)
    {
        for (int component = 0; component < 3; component++)
        {
            float scale = MathF.Max(1F, MathF.Abs(expected[component]));
            Assert.True(
                DirectComparer.Equals(expected[component] / scale, actual[component] / scale),
                $"{context}, component={component}, expected={expected[component]:R}, actual={actual[component]:R}, "
                + $"Vector128={Vector128.IsHardwareAccelerated}, Vector256={Vector256.IsHardwareAccelerated}, "
                + $"Vector512={Vector512.IsHardwareAccelerated}, FMA={Fma.IsSupported}");
        }
    }

    /// <summary>
    /// Supplies alpha values whose exact bits must survive each color transfer.
    /// </summary>
    /// <param name="index">The input color index.</param>
    /// <returns>Signed zero, fractional alpha or a NaN with a fixed payload.</returns>
    private static float HdrAlpha(int index)
        => (index % 3) switch
        {
            0 => -0F,
            1 => .375F,
            _ => BitConverter.Int32BitsToSingle(unchecked((int)0xFFC12345))
        };
}
