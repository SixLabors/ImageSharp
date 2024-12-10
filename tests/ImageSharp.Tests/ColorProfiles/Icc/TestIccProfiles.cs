// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Wacton.Unicolour.Icc;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

internal static class TestIccProfiles
{
    private static readonly ConcurrentDictionary<string, IccProfile> ProfileCache = new();
    private static readonly ConcurrentDictionary<string, Wacton.Unicolour.Configuration> UnicolourConfigurationCache = new();

    public const string Fogra39 = "Coated_Fogra39L_VIGC_300.icc";       // v2 CMYK -> LAB, output, lut16
    public const string Swop2006 = "SWOP2006_Coated5v2.icc";            // v2 CMYK -> LAB, output, lut16
    public const string JapanColor2011 = "JapanColor2011Coated.icc";    // v2 CMYK -> LAB, output, lut8
    public const string Cgats21 = "CGATS21_CRPC7.icc";                  // v4 CMYK -> LAB, output, lutAToB: B-CLUT-A
    public const string RommRgb = "ISO22028-2_ROMM-RGB.icc";            // v4 RGB -> XYZ, colorspace, lutAToB: B-Matrix-M        [only intent 0]
    public const string StandardRgbV4 = "sRGB_v4_ICC_preference.icc";   // v4 RGB -> LAB, colorspace, lutAToB: B-Matrix-M-CLUT-A [only intent 0 & 1]
    public const string StandardRgbV2 = "sRGB2014.icc";                 // v2 RGB -> XYZ, display, TRCs

    public static IccProfile GetProfile(string file)
        => ProfileCache.GetOrAdd(file, f => new IccProfile(File.ReadAllBytes(GetFullPath(f))));

    public static Wacton.Unicolour.Configuration GetUnicolourConfiguration(string file)
        => UnicolourConfigurationCache.GetOrAdd(
            file,
            f => new Wacton.Unicolour.Configuration(iccConfiguration: new(GetFullPath(f), Intent.Unspecified, file)));

    private static string GetFullPath(string file)
        => Path.GetFullPath(Path.Combine(".", "TestDataIcc", "Profiles", file));
}
