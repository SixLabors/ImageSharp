// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// The well known standard illuminants.
/// Standard illuminants provide a basis for comparing images or colors recorded under different lighting
/// </summary>
/// <remarks>
/// Coefficients taken from: http://www.brucelindbloom.com/index.html?Eqn_ChromAdapt.html
/// and https://color.org/specification/ICC.1-2022-05.pdf
/// <br />
/// Descriptions taken from: http://en.wikipedia.org/wiki/Standard_illuminant
/// </remarks>
public static class KnownIlluminants
{
    /// <summary>
    /// Gets the Incandescent / Tungsten illuminant.
    /// </summary>
    public static CieXyz A { get; } = new(1.09850F, 1F, 0.35585F);

    /// <summary>
    /// Gets the Direct sunlight at noon (obsoleteF) illuminant.
    /// </summary>
    public static CieXyz B { get; } = new(0.99072F, 1F, 0.85223F);

    /// <summary>
    /// Gets the Average / North sky Daylight (obsoleteF) illuminant.
    /// </summary>
    public static CieXyz C { get; } = new(0.98074F, 1F, 1.18232F);

    /// <summary>
    /// Gets the Horizon Light.
    /// </summary>
    public static CieXyz D50 { get; } = new(0.96422F, 1F, 0.82521F);

    /// <summary>
    /// Gets the D50 illuminant used in the ICC profile specification.
    /// </summary>
    public static CieXyz D50Icc { get; } = new(0.9642F, 1F, 0.8249F);

    /// <summary>
    /// Gets the Mid-morning / Mid-afternoon Daylight illuminant.
    /// </summary>
    public static CieXyz D55 { get; } = new(0.95682F, 1F, 0.92149F);

    /// <summary>
    /// Gets the Noon Daylight: TelevisionF, sRGB color space illuminant.
    /// </summary>
    public static CieXyz D65 { get; } = new(0.95047F, 1F, 1.08883F);

    /// <summary>
    /// Gets the North sky Daylight illuminant.
    /// </summary>
    public static CieXyz D75 { get; } = new(0.94972F, 1F, 1.22638F);

    /// <summary>
    /// Gets the Equal energy illuminant.
    /// </summary>
    public static CieXyz E { get; } = new(1F, 1F, 1F);

    /// <summary>
    /// Gets the Cool White Fluorescent illuminant.
    /// </summary>
    public static CieXyz F2 { get; } = new(0.99186F, 1F, 0.67393F);

    /// <summary>
    /// Gets the D65 simulatorF, Daylight simulator illuminant.
    /// </summary>
    public static CieXyz F7 { get; } = new(0.95041F, 1F, 1.08747F);

    /// <summary>
    /// Gets the Philips TL84F, Ultralume 40 illuminant.
    /// </summary>
    public static CieXyz F11 { get; } = new(1.00962F, 1F, 0.64350F);
}
