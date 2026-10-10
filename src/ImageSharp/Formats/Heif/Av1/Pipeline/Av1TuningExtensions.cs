// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Groups the tunes that share the image quality tools.
/// </summary>
internal static class Av1TuningExtensions
{
    /// <summary>
    /// Returns whether a tune uses the image quality tools: the image quality tune or the SSIMULACRA 2 tune.
    /// </summary>
    /// <param name="tuning">The tune.</param>
    /// <returns>Whether the tune uses the image quality tools.</returns>
    public static bool IsImageTuning(this Av1Tuning tuning) => tuning is Av1Tuning.Iq or Av1Tuning.Ssimulacra2;
}
