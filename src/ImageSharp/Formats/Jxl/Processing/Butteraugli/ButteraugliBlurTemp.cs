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
    /// Allocates transposed scratch storage for every blur in a fixed-size comparison.
    /// </summary>
    /// <param name="configuration">The configuration that owns the sample allocation.</param>
    /// <param name="width">The comparison width.</param>
    /// <param name="height">The comparison height.</param>
    public ButteraugliBlurTemp(Configuration configuration, int width, int height)
        => this.TransposedTemp = new(configuration, height, width);

    /// <summary>
    /// Gets the owned transposed scratch plane. Callers borrow it until the next blur or disposal.
    /// </summary>
    public JxlPlane<float> TransposedTemp { get; }

    /// <inheritdoc />
    public void Dispose() => this.TransposedTemp.Dispose();
}
