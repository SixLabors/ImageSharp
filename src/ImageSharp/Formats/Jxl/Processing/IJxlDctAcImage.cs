// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Base DCT AC coefficient image
/// </summary>
internal interface IJxlDctAcImage
{
    /// <summary>
    /// Gets the bit width of AC coefficients
    /// </summary>
    public JxlDctAcType Type { get; }

    /// <summary>
    /// Gets the number of pixels per row.
    /// </summary>
    public int PixelsPerRow { get; }

    /// <summary>
    /// Gets a value indicating whether the image is empty and doesn't
    /// have anything within.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Returns a reference to the coefficients at the specified row.
    /// </summary>
    /// <param name="channel">The plane index (which channel).</param>
    /// <param name="y">The row index within plane specified by <paramref name="channel"/>.</param>
    /// <param name="xBase">The X offset at the specified row.</param>
    /// <returns>
    /// A reference to the coefficients inside the channel
    /// specified by index <paramref name="channel"/>, at index of
    /// the row specified by <paramref name="y"/>, with X offset
    /// specified by <paramref name="xBase"/>.
    /// </returns>
    public JxlDctAcPointer GetPlaneRow(int channel, int y, int xBase = 0);

    /// <summary>
    /// Returns a reference to the coefficients at the specified row.
    /// </summary>
    /// <param name="channel">The plane index (which channel).</param>
    /// <param name="y">The row index within plane specified by <paramref name="channel"/>.</param>
    /// <param name="xBase">The X offset at the specified row.</param>
    /// <returns>
    /// A reference to the coefficients inside the channel
    /// specified by index <paramref name="channel"/>, at index of
    /// the row specified by <paramref name="y"/>, with X offset
    /// specified by <paramref name="xBase"/>.
    /// </returns>
    /// <remarks>
    /// This is a read-only kind of <see cref="GetPlaneRow(int, int, int)"/>.
    /// </remarks>
    public JxlDctReadOnlyAcPointer GetReadOnlyPlaneRow(int channel, int y, int xBase = 0);

    /// <summary>
    /// Fills all planes with zero.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Fills everything within the specified plane with zero.
    /// </summary>
    /// <param name="plane">Desired index of the plane.</param>
    public void Clear(int plane = 0);
}
