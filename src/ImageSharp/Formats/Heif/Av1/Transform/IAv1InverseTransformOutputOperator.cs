// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines how inverse-transform residuals are added to a decoded sample representation.
/// </summary>
/// <remarks>
/// Residual lanes correspond to consecutive reconstructed samples. Implementations must widen packed predictions,
/// add and clip in signed 32-bit lanes, then store exactly four or eight results so callers do not require writable
/// padding beyond the transform block. The closed sample type allows byte and high-bit-depth storage to specialize.
/// </remarks>
/// <typeparam name="TSample">The decoded sample storage type.</typeparam>
internal interface IAv1InverseTransformOutputOperator<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Adds one residual to a predicted sample and clips the result to the coded bit depth.
    /// </summary>
    /// <param name="prediction">The predicted sample.</param>
    /// <param name="residual">The inverse-transform residual.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The reconstructed sample.</returns>
    public static abstract TSample Add(TSample prediction, int residual, int bitDepth);

    /// <summary>
    /// Adds four residuals to four predicted samples and stores the clipped results.
    /// </summary>
    /// <param name="prediction">The first predicted sample.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="residual">The four inverse-transform residuals.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static abstract void Add(ref TSample prediction, ref TSample destination, Vector128<int> residual, int bitDepth);

    /// <summary>
    /// Adds eight residuals to eight predicted samples and stores the clipped results.
    /// </summary>
    /// <param name="prediction">The first predicted sample.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="residual">The eight inverse-transform residuals.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static abstract void Add(ref TSample prediction, ref TSample destination, Vector256<int> residual, int bitDepth);
}
