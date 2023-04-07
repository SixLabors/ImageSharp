// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// A stack only struct used for reducing reference indirection during convolution operations.
/// </summary>
internal readonly ref struct ConvolutionState
{
    private readonly Span<int> rowOffsetMap;
    private readonly Span<int> columnOffsetMap;
    private readonly uint kernelHeight;
    private readonly uint kernelWidth;

    public ConvolutionState(
        in DenseMatrix<float> kernel,
        KernelSamplingMap map)
    {
        this.Kernel = new ReadOnlyKernel(kernel);
        this.kernelHeight = (uint)kernel.Rows;
        this.kernelWidth = (uint)kernel.Columns;
        this.rowOffsetMap = map.GetRowOffsetSpan();
        this.columnOffsetMap = map.GetColumnOffsetSpan();
    }

    public readonly ReadOnlyKernel Kernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref int GetSampleRow(uint row)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.rowOffsetMap), row * this.kernelHeight);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref int GetSampleColumn(uint column)
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(this.columnOffsetMap), column * this.kernelWidth);
}
