// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// Owns one image's low, middle, high and ultra-high frequency bands for perceptual comparison.
/// </summary>
internal sealed class ButteraugliPsychoImage : IDisposable
{
    /// <summary>
    /// Owned ultra-high-frequency X and Y planes. Their entries remain stable while their sample allocations can be replaced.
    /// </summary>
    public InlineArray2<JxlPlane<float>> Uhf;

    /// <summary>
    /// Owned high-frequency X and Y planes. Frequency separation borrows the entries rather than replacing a copied array.
    /// </summary>
    public InlineArray2<JxlPlane<float>> Hf;

    /// <summary>
    /// Initializes empty owned planes without allocating sample storage.
    /// </summary>
    public ButteraugliPsychoImage()
    {
        for (int i = 0; i < 2; i++)
        {
            this.Hf[i] = new();
            this.Uhf[i] = new();
        }
    }

    /// <summary>
    /// Gets the owned middle-frequency XYB image.
    /// </summary>
    public JxlImage3<float> Mf { get; } = new();

    /// <summary>
    /// Gets the owned low-frequency XYB image.
    /// </summary>
    public JxlImage3<float> Lf { get; } = new();

    /// <inheritdoc />
    public void Dispose()
    {
        for (int i = 0; i < 2; i++)
        {
            this.Hf[i].Dispose();
            this.Uhf[i].Dispose();
        }

        this.Mf.Dispose();
        this.Lf.Dispose();
    }
}
