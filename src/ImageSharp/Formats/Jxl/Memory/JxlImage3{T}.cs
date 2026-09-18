// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

// NOTE: Do not seal this class.
internal class JxlImage3<T> : IDisposable
    where T : unmanaged
{
    private sealed class TypeChangingMemoryManager<TTarget>(Memory<byte> memory) : MemoryManager<TTarget>
        where TTarget : unmanaged
    {
        public override Span<TTarget> GetSpan() => MemoryMarshal.Cast<byte, TTarget>(memory.Span);

        // we don't use these
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotImplementedException();

        public override void Unpin() => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
        }
    }

    private const int PlaneCount = 3;

    private JxlPlane<T>[] planes = new JxlPlane<T>[3];

    public JxlImage3()
    {
    }

    public JxlImage3(Configuration configuration, int xSize, int ySize)
        => this.Allocate(configuration, xSize, ySize);

    public JxlImage3(JxlImage3<T> other)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            this.planes[i] = other.planes[i];
        }
    }

    public int XSize => this.planes[0].XSize;

    public int YSize => this.planes[0].YSize;

    public int BytesPerRow => this.planes[0].BytesPerRow;

    public int PixelsPerRow => this.planes[0].PixelsPerRow;

    public Span<T> PlaneRow(int plane, int row)
    {
        this.PlaneRowBoundsCheck(plane, row);

        int rowOffset = row * this.planes[0].BytesPerRow;
        Span<T> rowSpan = MemoryMarshal.Cast<byte, T>(this.planes[plane].BytesSpan[rowOffset..]);

        return rowSpan;
    }

    // This method performs minor allocations!
    public Memory<T> PlaneRowMemory(int plane, int row)
    {
        this.PlaneRowBoundsCheck(plane, row);

        int rowOffset = row * this.planes[0].BytesPerRow;
        Memory<byte> rowMemoryBytes = this.planes[plane].Bytes[rowOffset..];

        // we have to allocate a utility class so we can reinterpret
        // a Memory<T>.
        // Unsafe.As is truly unsafe because, f.e. what if there are
        // 400 bytes but T is 4 bytes? the length will remain as 400.
        TypeChangingMemoryManager<T> reinterpreter = new(rowMemoryBytes);
        return reinterpreter.Memory;
    }

    public Span<T> PlaneRow(Rectangle rectangle, int c, int y)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(y + rectangle.Top, 0, nameof(y));

        return this.PlaneRow(c, y + rectangle.Top)[rectangle.Left..];
    }

    public JxlPlane<T> Plane(int index) => this.planes[index];

    public void Swap(JxlImage3<T> other)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            other.planes[i].Swap(this.planes[i]);
        }
    }

    public static JxlImage3<T> Create(Configuration configuration, int xSize, int ySize)
        => new(configuration, xSize, ySize);

    public void Allocate(Configuration configuration, int xSize, int ySize)
    {
        JxlPlane<T> plane0 = JxlPlane<T>.Create(configuration, xSize, ySize);
        JxlPlane<T> plane1 = JxlPlane<T>.Create(configuration, xSize, ySize);
        JxlPlane<T> plane2 = JxlPlane<T>.Create(configuration, xSize, ySize);

        this.planes = [plane0, plane1, plane2];
    }

    public bool ShrinkTo(int x, int y)
    {
        for (int i = 0; i < PlaneCount; i++)
        {
            if (!this.planes[i].ShrinkTo(x, y))
            {
                return false;
            }
        }

        return true;
    }

    private void PlaneRowBoundsCheck(int c, int y)
    {
        DebugGuard.MustBeLessThan(c, PlaneCount, nameof(c));
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));
    }

    /// <summary>
    /// Returns the rectangle for this image bounds.
    /// </summary>
    /// <returns>A rectangle with x,y=0,0 width,height=XSize,YSize.</returns>
    public Rectangle GetRectangle() => new(0, 0, this.XSize, this.YSize);

    public void Dispose()
    {
        foreach (JxlPlane<T> plane in this.planes)
        {
            plane.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
