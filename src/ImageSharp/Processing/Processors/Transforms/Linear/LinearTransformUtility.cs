// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Utility methods for linear transforms.
/// </summary>
internal static class LinearTransformUtility
{
    /// <summary>
    /// Returns the sampling radius for the given sampler and dimensions.
    /// </summary>
    /// <typeparam name="TResampler">The type of resampler.</typeparam>
    /// <param name="sampler">The resampler sampler.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="destinationSize">The destination size.</param>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static float GetSamplingRadius<TResampler>(in TResampler sampler, int sourceSize, int destinationSize)
         where TResampler : struct, IResampler
    {
        float scale = (float)sourceSize / destinationSize;

        if (scale < 1F)
        {
            scale = 1F;
        }

        return MathF.Ceiling(sampler.Radius * scale);
    }

    /// <summary>
    /// Gets the start position (inclusive) for a sampling range given
    /// the radius, center position and max constraint.
    /// </summary>
    /// <param name="radius">The radius.</param>
    /// <param name="center">The center position.</param>
    /// <param name="min">The min allowed amount.</param>
    /// <param name="max">The max allowed amount.</param>
    /// <returns>The <see cref="int"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int GetRangeStart(float radius, float center, int min, int max)
        => Numerics.Clamp((int)MathF.Floor(center - radius), min, max);

    /// <summary>
    /// Gets the end position (inclusive) for a sampling range given
    /// the radius, center position and max constraint.
    /// </summary>
    /// <param name="radius">The radius.</param>
    /// <param name="center">The center position.</param>
    /// <param name="min">The min allowed amount.</param>
    /// <param name="max">The max allowed amount.</param>
    /// <returns>The <see cref="int"/>.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int GetRangeEnd(float radius, float center, int min, int max)
        => Numerics.Clamp((int)MathF.Ceiling(center + radius), min, max);
}
