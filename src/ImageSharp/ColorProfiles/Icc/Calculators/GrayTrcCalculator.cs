// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

/// <summary>
/// Converts between a grayscale device channel and the achromatic axis of its ICC PCS.
/// </summary>
internal class GrayTrcCalculator : IVector4Calculator
{
    // Encode the fixed D50 white point once so each XYZ sample needs only the gray multiplication.
    private static readonly Vector3 ScaledD50 = KnownIlluminants.D50Icc.ToScaledVector4().AsVector3();

    private readonly ISingleCalculator calculator;
    private readonly bool toPcs;
    private readonly bool isLab;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrayTrcCalculator"/> class.
    /// </summary>
    /// <param name="grayTrc">The grayscale tone response curve.</param>
    /// <param name="pcsType">The profile's XYZ or Lab connection space.</param>
    /// <param name="toPcs">Whether to convert device gray to the PCS instead of the reverse.</param>
    public GrayTrcCalculator(IccTagDataEntry grayTrc, IccColorSpaceType pcsType, bool toPcs)
    {
        // A grayscale profile has one curve, so use its scalar calculator without channel arrays or traversal.
        this.calculator = grayTrc switch
        {
            IccCurveTagDataEntry curve => new CurveCalculator(curve, !toPcs),
            IccParametricCurveTagDataEntry parametricCurve => new ParametricCurveCalculator(parametricCurve, !toPcs),
            _ => throw new InvalidIccProfileException("Invalid Entry."),
        };

        this.toPcs = toPcs;
        this.isLab = pcsType == IccColorSpaceType.CieLab;
    }

    /// <summary>
    /// Converts a normalized gray channel to encoded PCS values, or encoded PCS values to device gray.
    /// </summary>
    /// <param name="value">The device value in X, or the encoded PCS components.</param>
    /// <returns>The encoded PCS value, or the device gray value in X.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Calculate(Vector4 value)
    {
        if (this.toPcs)
        {
            float gray = this.calculator.Calculate(value.X);

            // ICC monochrome TRCs describe the achromatic PCS axis: relative Y for XYZ or L*/100 for Lab.
            // Construct all three PCS components from that one result; the unused device lanes are not XYZ.
            if (this.isLab)
            {
                // L*/100 is already normalized; neutral a* and b* encode as (0 + 128)/255.
                return new Vector4(gray, 128F / 255F, 128F / 255F, 1F);
            }

            return new Vector4(ScaledD50 * gray, 1F);
        }

        // Encoded Lab already stores L*/100 in X. XYZ must be decoded before selecting its Y component;
        // applying the inverse curve to X would instead interpret the tristimulus X value as luminance.
        // The 65535/32768 factor reverses the ICC XYZ encoding; the unused X and Z components need no scaling.
        float luminance = this.isLab ? value.X : value.Y * (65535F / 32768F);
        return new Vector4(this.calculator.Calculate(luminance));
    }
}
