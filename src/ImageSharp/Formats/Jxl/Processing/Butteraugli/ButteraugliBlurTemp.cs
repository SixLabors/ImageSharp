// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

/// <summary>
/// Owns the reusable transposed plane used by Butteraugli's two-pass blur.
/// </summary>
internal sealed class ButteraugliBlurTemp : IDisposable
{
    /// <summary>
    /// Gets the owned transposed scratch plane. Callers borrow it until the next blur or disposal.
    /// </summary>
    public JxlPlane<float> TransposedTemp { get; } = new();

    /// <summary>
    /// Reuses or replaces the scratch allocation for the transposed input dimensions.
    /// </summary>
    /// <param name="configuration">The allocator configuration used if the existing capacity is too small.</param>
    /// <param name="input">The input plane whose dimensions are transposed.</param>
    /// <returns>The borrowed scratch plane, with width equal to the input height and height equal to its width.</returns>
    public JxlPlane<float> GetTransposed(Configuration configuration, JxlPlane<float> input)
    {
        if (!this.TransposedTemp.ShrinkTo(input.YSize, input.XSize))
        {
            // Allocate before exchanging ownership. Disposal then releases the old allocation, including on a later blur failure.
            using JxlPlane<float> replacement = new(configuration, input.YSize, input.XSize);
            this.TransposedTemp.Swap(replacement);
        }

        return this.TransposedTemp;
    }

    /// <inheritdoc />
    public void Dispose() => this.TransposedTemp.Dispose();
}
