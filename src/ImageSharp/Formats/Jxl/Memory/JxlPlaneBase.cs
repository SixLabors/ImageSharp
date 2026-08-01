// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

// NOTE: Do not seal this type.
internal class JxlPlaneBase : IDisposable
{
    private IMemoryOwner<byte>? bytes;

    public JxlPlaneBase(int xSize, int ySize, int sizeOfT)
    {
        this.XSize = xSize;
        this.YSize = ySize;
        this.OriginalXSize = xSize;
        this.OriginalYSize = ySize;
        this.BytesPerRow = 0;
        this.Size = sizeOfT;
    }

    public JxlPlaneBase()
        : this(0, 0, 0)
    {
    }

    public int BytesPerRow { get; private set; }

    public int XSize { get; private set; }

    public int YSize { get; private set; }

    public Memory<byte> Bytes =>
#if DEBUG
        this.bytes?.Memory ?? throw new InvalidOperationException("Bytes are missing");
#else
        return this.bytes!.Memory;
#endif

    public Span<byte> BytesSpan => this.Bytes.Span;

    protected int Size { get; set; }

    protected int OriginalXSize { get; set; }

    protected int OriginalYSize { get; set; }

    public bool Allocate(Configuration configuration, int prePadding)
    {
        if (this.bytes != null || this.BytesPerRow != 0)
        {
            return false;
        }

        if (this.XSize == 0 || this.YSize == 0)
        {
            return true;
        }

        int totalBytes = unchecked(this.YSize * this.BytesPerRow);

        this.bytes = configuration.MemoryAllocator.Allocate<byte>(totalBytes + (prePadding * this.Size));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShrinkTo(int x, int y)
    {
        if (x <= this.OriginalXSize || y <= this.OriginalYSize)
        {
            return false;
        }

        DebugGuard.MustBeLessThanOrEqualTo(x, this.OriginalXSize, nameof(x));
        DebugGuard.MustBeLessThanOrEqualTo(y, this.OriginalYSize, nameof(y));

        this.XSize = x;
        this.YSize = y;

        return true;
    }

    protected Span<T> GetRowBase<T>(int y)
        where T : unmanaged
    {
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));

        Span<byte> row = this.Bytes.Span[(y * this.BytesPerRow)..];
        return MemoryMarshal.Cast<byte, T>(row);
    }

    protected void SetBytes(IMemoryOwner<byte> bytes) => this.bytes = bytes;

    public void Swap(JxlPlaneBase other)
    {
        (this.XSize, other.XSize) = (other.XSize, this.XSize);
        (this.YSize, other.YSize) = (other.YSize, this.YSize);
        (this.OriginalXSize, other.OriginalXSize) = (other.OriginalXSize, this.OriginalXSize);
        (this.OriginalYSize, other.OriginalYSize) = (other.OriginalYSize, this.OriginalYSize);
        (this.BytesPerRow, other.BytesPerRow) = (other.BytesPerRow, this.BytesPerRow);
        (this.bytes, other.bytes) = (other.bytes, this.bytes);
    }

    public void Dispose()
    {
        this.bytes?.Dispose();
        GC.SuppressFinalize(this);
    }
}
