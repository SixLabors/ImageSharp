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

    public const string Fogra39 = "Coated_Fogra39L_VIGC_300.icc";

    public const string Swop2006 = "SWOP2006_Coated5v2.icc";

    public static IccProfile GetProfile(string file)
        => ProfileCache.GetOrAdd(file, f => new IccProfile(File.ReadAllBytes(GetFullPath(f))));

    public static Wacton.Unicolour.Configuration GetUnicolourConfiguration(string file)
        => UnicolourConfigurationCache.GetOrAdd(
            file,
            f => new Wacton.Unicolour.Configuration(iccConfiguration: new(GetFullPath(f), Intent.Unspecified)));

    private static string GetFullPath(string file)
        => Path.GetFullPath(Path.Combine(".", "TestDataIcc", "Profiles", file));
}
