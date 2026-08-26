// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Color;

/// <summary>
/// Exposes the native planar sample layout shared by HEIF image codecs.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal interface IHeifPlanarSampleBuffer<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Gets the full luma-plane width in samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the full luma-plane height in samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the luma sample precision in bits.
    /// </summary>
    public int LumaBitDepth { get; }

    /// <summary>
    /// Gets the chroma sample precision in bits.
    /// </summary>
    public int ChromaBitDepth { get; }

    /// <summary>
    /// Gets a value indicating whether the buffer contains only the luma plane.
    /// </summary>
    public bool IsMonochrome { get; }

    /// <summary>
    /// Gets the horizontal chroma subsampling shift.
    /// </summary>
    public int ChromaSubsamplingX { get; }

    /// <summary>
    /// Gets the vertical chroma subsampling shift.
    /// </summary>
    public int ChromaSubsamplingY { get; }

    /// <summary>
    /// Gets the horizontal chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionX { get; }

    /// <summary>
    /// Gets the vertical chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionY { get; }

    /// <summary>
    /// Gets one complete writable row from the luma, green, or intensity plane.
    /// </summary>
    /// <param name="row">The zero-based row index in plane samples.</param>
    /// <returns>The native luma, green, or intensity samples.</returns>
    public Span<TSample> GetLumaRowSpan(int row);

    /// <summary>
    /// Gets one complete writable row from the blue-difference, blue, or first opponent-color plane.
    /// </summary>
    /// <param name="row">The zero-based row index in plane samples.</param>
    /// <returns>The native blue-difference, blue, or first opponent-color samples.</returns>
    public Span<TSample> GetChromaBlueRowSpan(int row);

    /// <summary>
    /// Gets one complete writable row from the red-difference, red, or second opponent-color plane.
    /// </summary>
    /// <param name="row">The zero-based row index in plane samples.</param>
    /// <returns>The native red-difference, red, or second opponent-color samples.</returns>
    public Span<TSample> GetChromaRedRowSpan(int row);
}
