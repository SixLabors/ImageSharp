// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Identifies the intra-prediction modes used by an AV1 still-picture frame.
/// </summary>
/// <remarks>Inter modes are omitted because reduced still-picture frames do not reference other frames.</remarks>
internal enum Av1PredictionMode
{
    /// <summary>
    /// Predicts each sample from the average of the available top and left neighbors.
    /// </summary>
    DC,

    /// <summary>
    /// Repeats the top neighboring row vertically through the block.
    /// </summary>
    Vertical,

    /// <summary>
    /// Repeats the left neighboring column horizontally through the block.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Projects neighboring samples into the block at 45 degrees.
    /// </summary>
    Directional45Degrees,

    /// <summary>
    /// Projects neighboring samples into the block at 135 degrees.
    /// </summary>
    Directional135Degrees,

    /// <summary>
    /// Projects neighboring samples into the block at 113 degrees.
    /// </summary>
    Directional113Degrees,

    /// <summary>
    /// Projects neighboring samples into the block at 157 degrees.
    /// </summary>
    Directional157Degrees,

    /// <summary>
    /// Projects neighboring samples into the block at 203 degrees.
    /// </summary>
    Directional203Degrees,

    /// <summary>
    /// Projects neighboring samples into the block at 67 degrees.
    /// </summary>
    Directional67Degrees,

    /// <summary>
    /// Blends horizontal and vertical smooth predictions.
    /// </summary>
    Smooth,

    /// <summary>
    /// Interpolates vertically between the top row and the bottom-left neighbor.
    /// </summary>
    SmoothVertical,

    /// <summary>
    /// Interpolates horizontally between the left column and the top-right neighbor.
    /// </summary>
    SmoothHorizontal,

    /// <summary>
    /// Selects the neighbor with the smallest gradient from the top-left reference.
    /// </summary>
    Paeth,

    /// <summary>
    /// Predicts chroma from the reconstructed luma AC surface.
    /// </summary>
    UvChromaFromLuma,

    /// <summary>
    /// The first luma intra-prediction mode.
    /// </summary>
    IntraModeStart = DC,

    /// <summary>
    /// The exclusive upper bound of luma intra-prediction modes.
    /// </summary>
    IntraModeEnd = Paeth + 1,

    /// <summary>
    /// The number of luma intra-prediction modes.
    /// </summary>
    IntraModes = Paeth + 1,

    /// <summary>
    /// The number of chroma intra-prediction modes, including chroma-from-luma.
    /// </summary>
    UvIntraModes = UvChromaFromLuma + 1,

    /// <summary>
    /// The invalid intra-mode sentinel matching the complete AV1 prediction-mode domain.
    /// </summary>
    IntraInvalid = 25,
}
