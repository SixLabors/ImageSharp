// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal sealed class Av1LevelBuffer : IDisposable
{
    private IMemoryOwner<byte>? memory;

    public Av1LevelBuffer(Configuration configuration)
        : this(configuration, new Size(Av1Constants.MaxTransformSize, Av1Constants.MaxTransformSize))
    {
    }

    public Av1LevelBuffer(Configuration configuration, Size size)
    {
        this.Size = size;
        int totalHeight = Av1Constants.TransformPadTop + size.Height + Av1Constants.TransformPadBottom;
        this.Stride = Av1Constants.TransformPadHorizontal + size.Width;
        this.memory = configuration.MemoryAllocator.Allocate<byte>(this.Stride * totalHeight, AllocationOptions.Clean);
    }

    public Size Size { get; }

    public int Stride { get; }

    public void Initialize(Span<int> coefficientBuffer)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(coefficientBuffer.Length, this.Size.Width * this.Size.Height, nameof(coefficientBuffer));
        for (int y = 0; y < this.Size.Height; y++)
        {
            ref byte destRef = ref this.GetRow(y)[0];
            ref int sourceRef = ref coefficientBuffer[y * this.Size.Width];
            for (int x = 0; x < this.Size.Width; x++)
            {
                destRef = (byte)Av1Math.Clamp(sourceRef, 0, byte.MaxValue);
                destRef = ref Unsafe.Add(ref destRef, 1);
                sourceRef = ref Unsafe.Add(ref sourceRef, 1);
            }
        }
    }

    public Span<byte> GetRow(int y)
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(y, -Av1Constants.TransformPadTop);
        int row = y + Av1Constants.TransformPadTop;
        return this.memory.Memory.Span.Slice(row * this.Stride, this.Size.Width);
    }

    public void Dispose()
    {
        this.memory?.Dispose();
        this.memory = null;
    }

    internal void Clear()
    {
        ObjectDisposedException.ThrowIf(this.memory == null, this);
        this.memory.Memory.Span.Clear();
    }

    internal Span<byte> GetPaddedRow(int index, int blockWidthLog2)
    {
        int y = index >> blockWidthLog2;
        return this.GetRow(y);
    }
}
