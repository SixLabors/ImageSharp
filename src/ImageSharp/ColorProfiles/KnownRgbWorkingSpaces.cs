// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Chromaticity coordinates based on: <see href="http://www.brucelindbloom.com/index.html?WorkingSpaceInfo.html"/>
/// </summary>
public static class KnownRgbWorkingSpaces
{
    /// <summary>
    /// sRgb working space.
    /// </summary>
    /// <remarks>
    /// Uses proper companding function, according to:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_Rgb_to_XYZ.html"/>
    /// </remarks>
    public static readonly RgbWorkingSpace SRgb = new SRgbWorkingSpace(KnownIlluminants.D65, new(new(0.6400F, 0.3300F), new(0.3000F, 0.6000F), new(0.1500F, 0.0600F)));

    /// <summary>
    /// Simplified sRgb working space (uses <see cref="GammaCompanding">gamma companding</see> instead of <see cref="SRgbCompanding"/>).
    /// See also <see cref="SRgb"/>.
    /// </summary>
    public static readonly RgbWorkingSpace SRgbSimplified = new GammaWorkingSpace(2.2F, KnownIlluminants.D65, new(new(0.6400F, 0.3300F), new(0.3000F, 0.6000F), new(0.1500F, 0.0600F)));

    /// <summary>
    /// Rec. 709 (ITU-R Recommendation BT.709) working space.
    /// </summary>
    public static readonly RgbWorkingSpace Rec709 = new Rec709WorkingSpace(KnownIlluminants.D65, new(new(0.64F, 0.33F), new(0.30F, 0.60F), new(0.15F, 0.06F)));

    /// <summary>
    /// Rec. 2020 (ITU-R Recommendation BT.2020F) working space.
    /// </summary>
    public static readonly RgbWorkingSpace Rec2020 = new Rec2020WorkingSpace(KnownIlluminants.D65, new(new(0.708F, 0.292F), new(0.170F, 0.797F), new(0.131F, 0.046F)));

    /// <summary>
    /// ECI Rgb v2 working space.
    /// </summary>
    public static readonly RgbWorkingSpace ECIRgbv2 = new LWorkingSpace(KnownIlluminants.D50, new(new(0.6700F, 0.3300F), new(0.2100F, 0.7100F), new(0.1400F, 0.0800F)));

    /// <summary>
    /// Adobe Rgb (1998) working space.
    /// </summary>
    public static readonly RgbWorkingSpace AdobeRgb1998 = new GammaWorkingSpace(2.2F, KnownIlluminants.D65, new(new(0.6400F, 0.3300F), new(0.2100F, 0.7100F), new(0.1500F, 0.0600F)));

    /// <summary>
    /// Apple sRgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace ApplesRgb = new GammaWorkingSpace(1.8F, KnownIlluminants.D65, new(new(0.6250F, 0.3400F), new(0.2800F, 0.5950F), new(0.1550F, 0.0700F)));

    /// <summary>
    /// Best Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace BestRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D50, new(new(0.7347F, 0.2653F), new(0.2150F, 0.7750F), new(0.1300F, 0.0350F)));

    /// <summary>
    /// Beta Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace BetaRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D50, new(new(0.6888F, 0.3112F), new(0.1986F, 0.7551F), new(0.1265F, 0.0352F)));

    /// <summary>
    /// Bruce Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace BruceRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D65, new(new(0.6400F, 0.3300F), new(0.2800F, 0.6500F), new(0.1500F, 0.0600F)));

    /// <summary>
    /// CIE Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace CIERgb = new GammaWorkingSpace(2.2F, KnownIlluminants.E, new(new(0.7350F, 0.2650F), new(0.2740F, 0.7170F), new(0.1670F, 0.0090F)));

    /// <summary>
    /// ColorMatch Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace ColorMatchRgb = new GammaWorkingSpace(1.8F, KnownIlluminants.D50, new(new(0.6300F, 0.3400F), new(0.2950F, 0.6050F), new(0.1500F, 0.0750F)));

    /// <summary>
    /// Don Rgb 4 working space.
    /// </summary>
    public static readonly RgbWorkingSpace DonRgb4 = new GammaWorkingSpace(2.2F, KnownIlluminants.D50, new(new(0.6960F, 0.3000F), new(0.2150F, 0.7650F), new(0.1300F, 0.0350F)));

    /// <summary>
    /// Ekta Space PS5 working space.
    /// </summary>
    public static readonly RgbWorkingSpace EktaSpacePS5 = new GammaWorkingSpace(2.2F, KnownIlluminants.D50, new(new(0.6950F, 0.3050F), new(0.2600F, 0.7000F), new(0.1100F, 0.0050F)));

    /// <summary>
    /// NTSC Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace NTSCRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.C, new(new(0.6700F, 0.3300F), new(0.2100F, 0.7100F), new(0.1400F, 0.0800F)));

    /// <summary>
    /// PAL/SECAM Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace PALSECAMRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D65, new(new(0.6400F, 0.3300F), new(0.2900F, 0.6000F), new(0.1500F, 0.0600F)));

    /// <summary>
    /// ProPhoto Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace ProPhotoRgb = new GammaWorkingSpace(1.8F, KnownIlluminants.D50, new(new(0.7347F, 0.2653F), new(0.1596F, 0.8404F), new(0.0366F, 0.0001F)));

    /// <summary>
    /// SMPTE-C Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace SMPTECRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D65, new(new(0.6300F, 0.3400F), new(0.3100F, 0.5950F), new(0.1550F, 0.0700F)));

    /// <summary>
    /// Wide Gamut Rgb working space.
    /// </summary>
    public static readonly RgbWorkingSpace WideGamutRgb = new GammaWorkingSpace(2.2F, KnownIlluminants.D50, new(new(0.7350F, 0.2650F), new(0.1150F, 0.8260F), new(0.1570F, 0.0180F)));
}
