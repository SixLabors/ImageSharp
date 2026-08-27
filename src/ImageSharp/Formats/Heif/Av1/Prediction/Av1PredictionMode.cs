// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Identifies the luma intra and inter prediction modes used by an AV1 coding block.
/// </summary>
internal enum Av1PredictionMode : byte
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
    /// Uses the nearest motion-vector candidate for one reference frame.
    /// </summary>
    NearestMotionVector = 13,

    /// <summary>
    /// Uses a near motion-vector candidate for one reference frame.
    /// </summary>
    NearMotionVector = 14,

    /// <summary>
    /// Uses the global-motion model for one reference frame.
    /// </summary>
    GlobalMotionVector = 15,

    /// <summary>
    /// Decodes a new motion vector for one reference frame.
    /// </summary>
    NewMotionVector = 16,

    /// <summary>
    /// Uses the nearest motion-vector candidate for both compound references.
    /// </summary>
    NearestNearestMotionVector = 17,

    /// <summary>
    /// Uses a near motion-vector candidate for both compound references.
    /// </summary>
    NearNearMotionVector = 18,

    /// <summary>
    /// Uses the nearest candidate for the first compound reference and decodes a new vector for the second.
    /// </summary>
    NearestNewMotionVector = 19,

    /// <summary>
    /// Decodes a new vector for the first compound reference and uses the nearest candidate for the second.
    /// </summary>
    NewNearestMotionVector = 20,

    /// <summary>
    /// Uses a near candidate for the first compound reference and decodes a new vector for the second.
    /// </summary>
    NearNewMotionVector = 21,

    /// <summary>
    /// Decodes a new vector for the first compound reference and uses a near candidate for the second.
    /// </summary>
    NewNearMotionVector = 22,

    /// <summary>
    /// Uses the global-motion model for both compound references.
    /// </summary>
    GlobalGlobalMotionVector = 23,

    /// <summary>
    /// Decodes a new motion vector for both compound references.
    /// </summary>
    NewNewMotionVector = 24,

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
    /// The first single-reference inter-prediction mode.
    /// </summary>
    SingleInterModeStart = NearestMotionVector,

    /// <summary>
    /// The exclusive upper bound of single-reference inter-prediction modes.
    /// </summary>
    SingleInterModeEnd = NearestNearestMotionVector,

    /// <summary>
    /// The first compound-reference inter-prediction mode.
    /// </summary>
    CompoundInterModeStart = NearestNearestMotionVector,

    /// <summary>
    /// The exclusive upper bound of compound-reference inter-prediction modes.
    /// </summary>
    CompoundInterModeEnd = NewNewMotionVector + 1,

    /// <summary>
    /// The first inter-prediction mode.
    /// </summary>
    InterModeStart = NearestMotionVector,

    /// <summary>
    /// The exclusive upper bound of all inter-prediction modes.
    /// </summary>
    InterModeEnd = NewNewMotionVector + 1,

    /// <summary>
    /// The number of luma and inter prediction modes in the complete AV1 mode domain.
    /// </summary>
    PredictionModeCount = NewNewMotionVector + 1,

    /// <summary>
    /// The invalid intra-mode sentinel matching the complete AV1 prediction-mode domain.
    /// </summary>
    IntraInvalid = PredictionModeCount,
}
