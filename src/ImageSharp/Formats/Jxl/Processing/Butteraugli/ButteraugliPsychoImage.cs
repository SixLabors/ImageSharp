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

    /// <summary>
    /// Allocates every frequency band before comparisons begin, without replacing the owned plane wrappers.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocations.</param>
    /// <param name="width">The comparison width in samples.</param>
    /// <param name="height">The comparison height in rows.</param>
    /// <remarks>The containing comparator releases partially allocated bands if setup fails.</remarks>
    public void Allocate(Configuration configuration, int width, int height)
    {
        using JxlImage3<float> middle = new(configuration, width, height);
        this.Mf.Swap(middle);

        using JxlImage3<float> low = new(configuration, width, height);
        this.Lf.Swap(low);

        for (int c = 0; c < 2; c++)
        {
            using JxlPlane<float> high = new(configuration, width, height);
            this.Hf[c].Swap(high);

            using JxlPlane<float> ultraHigh = new(configuration, width, height);
            this.Uhf[c].Swap(ultraHigh);
        }
    }

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
