// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks;

/// <summary>
/// Measures profile recognition after profile parsing and shared table initialization.
/// </summary>
[Config(typeof(Config.Short))]
public class IccProfileRecognition
{
    private IccProfile knownProfile;
    private IccProfile linearProfile;
    private IccProfile sampledProfile;

    /// <summary>
    /// Loads stored profiles and initializes their parsed entries before measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string profilePath = Path.Combine(TestEnvironment.SolutionDirectoryFullPath, "tests/ImageSharp.Tests/TestDataIcc/Profiles/sRGB2014.icc");
        this.knownProfile = new IccProfile(File.ReadAllBytes(profilePath));
        this.sampledProfile = new IccProfile(this.knownProfile.ToByteArray());
        this.sampledProfile.Header.Id = default;
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Preserve };
        using Image<Rgb24> image = Image.Load<Rgb24>(options, Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Icc.LinearRgb));
        this.linearProfile = image.Metadata.IccProfile;

        // Parsing and the shared sRGB table are setup costs. The measured calls include the full
        // recognition logic, including sequence comparisons and sampled-curve validation.
        _ = this.knownProfile.IsCanonicalSrgbMatrixTrc();
        _ = this.linearProfile.IsCanonicalSrgbMatrixTrc();
        _ = this.sampledProfile.IsCanonicalSrgbMatrixTrc();
    }

    /// <summary>
    /// Checks an sRGB profile with a recognized identifier.
    /// </summary>
    /// <returns>Whether the profile is canonical sRGB.</returns>
    [Benchmark]
    public bool KnownProfileId() => this.knownProfile.IsCanonicalSrgbMatrixTrc();

    /// <summary>
    /// Rejects the stored linear profile that reproduced issue 3216.
    /// </summary>
    /// <returns>Whether the profile is canonical sRGB.</returns>
    [Benchmark]
    public bool ParametricLinearProfile() => this.linearProfile.IsCanonicalSrgbMatrixTrc();

    /// <summary>
    /// Checks every node and segment midpoint of a sampled sRGB curve without using the ID shortcut.
    /// </summary>
    /// <returns>Whether the profile is canonical sRGB.</returns>
    [Benchmark]
    public bool SampledSrgbProfile() => this.sampledProfile.IsCanonicalSrgbMatrixTrc();
}

/* Measured 2026-10-28, .NET 8.0.33. Profile parsing and shared-table initialization are outside measurement.

| Method                  |         Mean |       Error |     StdDev | Allocated |
| ----------------------- | -----------: | ----------: | ---------: | --------: |
| KnownProfileId          |     4.000 ns |   0.2302 ns |  0.0126 ns |         - |
| ParametricLinearProfile |    35.996 ns |   2.4142 ns |  0.1323 ns |         - |
| SampledSrgbProfile      | 3,770.766 ns | 199.6755 ns | 10.9449 ns |         - |
*/
