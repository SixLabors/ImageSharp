// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Source DCT block.
/// </summary>
internal readonly ref struct JxlDctSource(Span<float> data, int stride)
{
    /// <summary>
    /// Raw block data.
    /// </summary>
    public readonly Span<float> Data = data;

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
    public Span<float> Address(int row, int i) => this.Data[((row * this.Stride) + i)..];

    /// <summary>
    /// Returns the coefficient at the row and offset.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    /// <returns>
    /// Coefficient at that row and offset.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Read(int row, int i) => this.Data[(row * this.Stride) + i];

    /// <summary>
    /// Loads a vector at the specified row and offset.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    /// <returns>
    /// Vector at that row and offset.
    /// </returns>
    public Vector<float> LoadPart(int row, int i) => new(this.Address(row, i));

    /// <summary>
    /// Loads a vector at the specified row and offset.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    /// <returns>
    /// Vector at that row and offset.
    /// </returns>
    public Vector256<float> LoadPart256(int row, int i) => Vector256.Create<float>(this.Address(row, i));

    /// <summary>
    /// Loads a vector at the specified row and offset.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <param name="i">The offset.</param>
    /// <returns>
    /// Vector at that row and offset.
    /// </returns>
    public Vector128<float> LoadPart128(int row, int i) => Vector128.Create<float>(this.Address(row, i));
}
