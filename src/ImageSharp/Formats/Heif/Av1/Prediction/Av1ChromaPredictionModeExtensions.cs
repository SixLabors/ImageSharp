// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Provides luma-equivalent prediction metadata for AV1 chroma intra-prediction modes.
/// </summary>
internal static class Av1ChromaPredictionModeExtensions
{
    /// <summary>
    /// Maps a chroma intra-prediction mode to the equivalent luma intra-prediction mode.
    /// </summary>
    /// <param name="mode">The chroma intra-prediction mode.</param>
    /// <returns>The luma mode with the same spatial predictor, or the invalid luma sentinel for an invalid chroma mode.</returns>
    public static Av1PredictionMode ToLumaMode(this Av1ChromaPredictionMode mode)
        => mode switch
        {
            Av1ChromaPredictionMode.DC => Av1PredictionMode.DC,
            Av1ChromaPredictionMode.Vertical => Av1PredictionMode.Vertical,
            Av1ChromaPredictionMode.Horizontal => Av1PredictionMode.Horizontal,
            Av1ChromaPredictionMode.Directional45Degrees => Av1PredictionMode.Directional45Degrees,
            Av1ChromaPredictionMode.Directional135Degrees => Av1PredictionMode.Directional135Degrees,
            Av1ChromaPredictionMode.Directional113Degrees => Av1PredictionMode.Directional113Degrees,
            Av1ChromaPredictionMode.Directional157Degrees => Av1PredictionMode.Directional157Degrees,
            Av1ChromaPredictionMode.Directional203Degrees => Av1PredictionMode.Directional203Degrees,
            Av1ChromaPredictionMode.Directional67Degrees => Av1PredictionMode.Directional67Degrees,
            Av1ChromaPredictionMode.Smooth => Av1PredictionMode.Smooth,
            Av1ChromaPredictionMode.SmoothVertical => Av1PredictionMode.SmoothVertical,
            Av1ChromaPredictionMode.SmoothHorizontal => Av1PredictionMode.SmoothHorizontal,
            Av1ChromaPredictionMode.Paeth => Av1PredictionMode.Paeth,

            // Chroma-from-luma adds its AC contribution to a DC prediction. the reference decoder's get_uv_mode() therefore maps it
            // to DC when shared transform and neighbor metadata require the corresponding luma predictor.
            Av1ChromaPredictionMode.ChromaFromLuma => Av1PredictionMode.DC,
            _ => Av1PredictionMode.IntraInvalid,
        };

    /// <summary>
    /// Determines whether a chroma intra-prediction mode projects samples along a coded angle.
    /// </summary>
    /// <param name="mode">The chroma intra-prediction mode.</param>
    /// <returns><see langword="true"/> for a directional mode; otherwise, <see langword="false"/>.</returns>
    public static bool IsDirectional(this Av1ChromaPredictionMode mode)
        => mode is >= Av1ChromaPredictionMode.Vertical and <= Av1ChromaPredictionMode.Directional67Degrees;
}
