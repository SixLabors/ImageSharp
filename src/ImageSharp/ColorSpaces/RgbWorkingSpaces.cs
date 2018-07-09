// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Chromaticity coordinates taken from:
    /// <see href="http://www.brucelindbloom.com/index.html?WorkingSpaceInfo.html"/>
    /// </summary>
    internal static class RgbWorkingSpaces
    {
        /// <summary>
        /// sRgb working space.
        /// </summary>
        /// <remarks>
        /// Uses proper companding function, according to:
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_Rgb_to_XYZ.html"/>
        /// </remarks>
        public static readonly RgbWorkingSpace SRgb = new RgbWorkingSpace(Illuminants.D65, new SRgbCompanding(), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6400F, 0.3300F), new CieXyChromaticityCoordinates(0.3000F, 0.6000F), new CieXyChromaticityCoordinates(0.1500F, 0.0600F)));

        /// <summary>
        /// Simplified sRgb working space (uses <see cref="GammaCompanding">gamma companding</see> instead of <see cref="SRgbCompanding"/>).
        /// See also <see cref="SRgb"/>.
        /// </summary>
        public static readonly RgbWorkingSpace SRgbSimplified = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6400F, 0.3300F), new CieXyChromaticityCoordinates(0.3000F, 0.6000F), new CieXyChromaticityCoordinates(0.1500F, 0.0600F)));

        /// <summary>
        /// Rec. 709 (ITU-R Recommendation BT.709) working space.
        /// </summary>
        public static readonly RgbWorkingSpace Rec709 = new RgbWorkingSpace(Illuminants.D65, new Rec709Companding(), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.64F, 0.33F), new CieXyChromaticityCoordinates(0.30F, 0.60F), new CieXyChromaticityCoordinates(0.15F, 0.06F)));

        /// <summary>
        /// Rec. 2020 (ITU-R Recommendation BT.2020F) working space.
        /// </summary>
        public static readonly RgbWorkingSpace Rec2020 = new RgbWorkingSpace(Illuminants.D65, new Rec2020Companding(), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.708F, 0.292F), new CieXyChromaticityCoordinates(0.170F, 0.797F), new CieXyChromaticityCoordinates(0.131F, 0.046F)));

        /// <summary>
        /// ECI Rgb v2 working space.
        /// </summary>
        public static readonly RgbWorkingSpace ECIRgbv2 = new RgbWorkingSpace(Illuminants.D50, new LCompanding(), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6700F, 0.3300F), new CieXyChromaticityCoordinates(0.2100F, 0.7100F), new CieXyChromaticityCoordinates(0.1400F, 0.0800F)));

        /// <summary>
        /// Adobe Rgb (1998) working space.
        /// </summary>
        public static readonly RgbWorkingSpace AdobeRgb1998 = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6400F, 0.3300F), new CieXyChromaticityCoordinates(0.2100F, 0.7100F), new CieXyChromaticityCoordinates(0.1500F, 0.0600F)));

        /// <summary>
        /// Apple sRgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace ApplesRgb = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(1.8F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6250F, 0.3400F), new CieXyChromaticityCoordinates(0.2800F, 0.5950F), new CieXyChromaticityCoordinates(0.1550F, 0.0700F)));

        /// <summary>
        /// Best Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace BestRgb = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.7347F, 0.2653F), new CieXyChromaticityCoordinates(0.2150F, 0.7750F), new CieXyChromaticityCoordinates(0.1300F, 0.0350F)));

        /// <summary>
        /// Beta Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace BetaRgb = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6888F, 0.3112F), new CieXyChromaticityCoordinates(0.1986F, 0.7551F), new CieXyChromaticityCoordinates(0.1265F, 0.0352F)));

        /// <summary>
        /// Bruce Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace BruceRgb = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6400F, 0.3300F), new CieXyChromaticityCoordinates(0.2800F, 0.6500F), new CieXyChromaticityCoordinates(0.1500F, 0.0600F)));

        /// <summary>
        /// CIE Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace CIERgb = new RgbWorkingSpace(Illuminants.E, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.7350F, 0.2650F), new CieXyChromaticityCoordinates(0.2740F, 0.7170F), new CieXyChromaticityCoordinates(0.1670F, 0.0090F)));

        /// <summary>
        /// ColorMatch Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace ColorMatchRgb = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(1.8F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6300F, 0.3400F), new CieXyChromaticityCoordinates(0.2950F, 0.6050F), new CieXyChromaticityCoordinates(0.1500F, 0.0750F)));

        /// <summary>
        /// Don Rgb 4 working space.
        /// </summary>
        public static readonly RgbWorkingSpace DonRgb4 = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6960F, 0.3000F), new CieXyChromaticityCoordinates(0.2150F, 0.7650F), new CieXyChromaticityCoordinates(0.1300F, 0.0350F)));

        /// <summary>
        /// Ekta Space PS5 working space.
        /// </summary>
        public static readonly RgbWorkingSpace EktaSpacePS5 = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6950F, 0.3050F), new CieXyChromaticityCoordinates(0.2600F, 0.7000F), new CieXyChromaticityCoordinates(0.1100F, 0.0050F)));

        /// <summary>
        /// NTSC Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace NTSCRgb = new RgbWorkingSpace(Illuminants.C, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6700F, 0.3300F), new CieXyChromaticityCoordinates(0.2100F, 0.7100F), new CieXyChromaticityCoordinates(0.1400F, 0.0800F)));

        /// <summary>
        /// PAL/SECAM Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace PALSECAMRgb = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6400F, 0.3300F), new CieXyChromaticityCoordinates(0.2900F, 0.6000F), new CieXyChromaticityCoordinates(0.1500F, 0.0600F)));

        /// <summary>
        /// ProPhoto Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace ProPhotoRgb = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(1.8F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.7347F, 0.2653F), new CieXyChromaticityCoordinates(0.1596F, 0.8404F), new CieXyChromaticityCoordinates(0.0366F, 0.0001F)));

        /// <summary>
        /// SMPTE-C Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace SMPTECRgb = new RgbWorkingSpace(Illuminants.D65, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.6300F, 0.3400F), new CieXyChromaticityCoordinates(0.3100F, 0.5950F), new CieXyChromaticityCoordinates(0.1550F, 0.0700F)));

        /// <summary>
        /// Wide Gamut Rgb working space.
        /// </summary>
        public static readonly RgbWorkingSpace WideGamutRgb = new RgbWorkingSpace(Illuminants.D50, new GammaCompanding(2.2F), new RgbPrimariesChromaticityCoordinates(new CieXyChromaticityCoordinates(0.7350F, 0.2650F), new CieXyChromaticityCoordinates(0.1150F, 0.8260F), new CieXyChromaticityCoordinates(0.1570F, 0.0180F)));
    }
}