// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// A stack only struct used for reducing reference indirection during 2D convolution operations.
/// </summary>
internal readonly ref struct Convolution2DState
{
    private readonly Span<int> rowOffsetMap;
    private readonly Span<int> columnOffsetMap;
    private readonly uint kernelHeight;
    private readonly uint kernelWidth;

    public Convolution2DState(
        in DenseMatrix<float> kernelY,
        in DenseMatrix<float> kernelX,
        KernelSamplingMap map)
    {
        // We check the kernels are the same size upstream.
        this.KernelY = new ReadOnlyKernel(kernelY);
        this.KernelX = new ReadOnlyKernel(kernelX);
        this.kernelHeight = (uint)kernelY.Rows;
        this.kernelWidth = (uint)kernelY.Columns;
        this.rowOffsetMap = map.GetRowOffsetSpan();
        this.columnOffsetMap = map.GetColumnOffsetSpan();
    }

    public readonly ReadOnlyKernel KernelY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public readonly ReadOnlyKernel KernelX
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
