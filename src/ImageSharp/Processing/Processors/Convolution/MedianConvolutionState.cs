// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// A stack only struct used for reducing reference indirection during convolution operations.
/// </summary>
internal readonly ref struct MedianConvolutionState
{
    private readonly Span<int> rowOffsetMap;
    private readonly Span<int> columnOffsetMap;
    private readonly int kernelHeight;
    private readonly int kernelWidth;

    public MedianConvolutionState(
        in DenseMatrix<Vector4> kernel,
        KernelSamplingMap map)
    {
        this.Kernel = new Kernel<Vector4>(kernel);
        this.kernelHeight = kernel.Rows;
        this.kernelWidth = kernel.Columns;
        this.rowOffsetMap = map.GetRowOffsetSpan();
        this.columnOffsetMap = map.GetColumnOffsetSpan();
    }

    public readonly Kernel<Vector4> Kernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref int GetSampleRow(int row)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.rowOffsetMap), (uint)(row * this.kernelHeight));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref int GetSampleColumn(int column)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.columnOffsetMap), (uint)(column * this.kernelWidth));
}
