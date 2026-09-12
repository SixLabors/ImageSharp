// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Output DCT block.
/// </summary>
internal ref struct JxlDctOutput(Span<float> data, int stride)
{
    /// <summary>
    /// Raw block data.
    /// </summary>
    public Span<float> Data = data;

    /// <summary>
    /// Stride size.
    /// </summary>
    public readonly int Stride = stride;

    /// <summary>
    /// Returns the span to the start of a row and offset.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    /// <returns>
    /// Span for that row &amp; offset.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<float> Address(int row, int i) => this.Data[((row * this.Stride) + i)..];

    /// <summary>
    /// Writes a single value to the block at the row and offset.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value, int row, int i) => this.Data[(row * this.Stride) + i] = value;

    /// <summary>
    /// Stores the vector into the data at the specified row and offset.
    /// </summary>
    /// <param name="value">The vector to write.</param>
    /// <param name="row">The row index.</param>
    /// <param name="index">The offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void StorePart(Vector<float> value, int row, int index) => value.CopyTo(this.Address(row, index));

    /// <summary>
    /// Stores the vector into the data at the specified row and offset.
    /// </summary>
    /// <param name="value">The vector to write.</param>
    /// <param name="row">The row index.</param>
    /// <param name="index">The offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void StorePart256(Vector256<float> value, int row, int index) => value.CopyTo(this.Address(row, index));

    /// <summary>
    /// Stores the vector into the data at the specified row and offset.
    /// </summary>
    /// <param name="value">The vector to write.</param>
    /// <param name="row">The row index.</param>
    /// <param name="index">The offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void StorePart128(Vector128<float> value, int row, int index) => value.CopyTo(this.Address(row, index));
}
