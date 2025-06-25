// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Represents pixel component information within a pixel format.
/// </summary>
public readonly struct PixelComponentInfo
{
    private readonly long precisionData1;
    private readonly long precisionData2;

    private PixelComponentInfo(int count, int padding, long precisionData1, long precisionData2)
    {
        this.ComponentCount = count;
        this.Padding = padding;
        this.precisionData1 = precisionData1;
        this.precisionData2 = precisionData2;
    }

    /// <summary>
    /// Gets the number of components within the pixel.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the number of bytes of padding within the pixel.
    /// </summary>
    public int Padding { get; }

    /// <summary>
    /// Creates a new <see cref="PixelComponentInfo"/> instance.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="count">The number of components within the pixel format.</param>
    /// <param name="precision">The precision in bits of each component.</param>
    /// <returns>The <see cref="PixelComponentInfo"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The component precision and index cannot exceed the component range.</exception>
    public static PixelComponentInfo Create<TPixel>(int count, params int[] precision)
        where TPixel : unmanaged, IPixel<TPixel>
        => Create(count, Unsafe.SizeOf<TPixel>() * 8, precision);

    /// <summary>
    /// Creates a new <see cref="PixelComponentInfo"/> instance.
    /// </summary>
    /// <param name="count">The number of components within the pixel format.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="precision">The precision in bits of each component.</param>
    /// <returns>The <see cref="PixelComponentInfo"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The component precision and index cannot exceed the component range.</exception>
    public static PixelComponentInfo Create(int count, int bitsPerPixel, params int[] precision)
    {
        if (precision.Length < count || precision.Length > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"Count {count} must match the length of precision array and cannot exceed 16.");
        }

        long precisionData1 = 0;
        long precisionData2 = 0;
        int sum = 0;
        for (int i = 0; i < precision.Length; i++)
        {
            int p = precision[i];
            if (p is < 0 or > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(precision), $"Precision {precision.Length} must be between 0 and 255.");
            }

            if (i < 8)
            {
                precisionData1 |= ((long)p) << (8 * i);
            }
            else
            {
                precisionData2 |= ((long)p) << (8 * (i - 8));
            }

            sum += p;
        }

        return new PixelComponentInfo(count, bitsPerPixel - sum, precisionData1, precisionData2);
    }

    /// <summary>
    /// Returns the precision of the component in bits at the given index.
    /// </summary>
    /// <param name="componentIndex">The component index.</param>
    /// <returns>The <see cref="int"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The component index cannot exceed the component range.</exception>
    public int GetComponentPrecision(int componentIndex)
    {
        if (componentIndex < 0 || componentIndex >= this.ComponentCount)
        {
            throw new ArgumentOutOfRangeException($"Component index must be between 0 and {this.ComponentCount - 1} inclusive.");
        }

        long selectedPrecisionData = componentIndex < 8 ? this.precisionData1 : this.precisionData2;
        return (int)((selectedPrecisionData >> (8 * (componentIndex & 7))) & 0xFF);
    }

    /// <summary>
    /// Returns the maximum precision in bits of all components.
    /// </summary>
    /// <returns>The <see cref="int"/>.</returns>
    public int GetMaximumComponentPrecision()
    {
        int maxPrecision = 0;
        for (int i = 0; i < this.ComponentCount; i++)
        {
            int componentPrecision = this.GetComponentPrecision(i);
            if (componentPrecision > maxPrecision)
            {
                maxPrecision = componentPrecision;
            }
        }

        return maxPrecision;
    }
}
