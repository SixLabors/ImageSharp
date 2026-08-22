// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlDctAcImage<T> : IJxlDctAcImage, IDisposable
    where T : unmanaged
{
    private readonly JxlImage3<T> image;

    public unsafe JxlDctAcImage(Configuration configuration, int width, int height)
    {
        DebugGuard.IsTrue(sizeof(T) is 2 or 4, "The type must be 2 or 4 bytes");

        this.image = JxlImage3<T>.Create(configuration, width, height);
    }

    public unsafe JxlDctAcType Type => sizeof(T) == 4 ? JxlDctAcType.Ac32 : JxlDctAcType.Ac16;

    public int PixelsPerRow => this.image.PixelsPerRow;

    public bool IsEmpty => this.image.XSize == 0 || this.image.YSize == 0;

    public void Clear() => JxlImageOperations.ClearImage(this.image);

    public void Clear(int plane = 0) => JxlImageOperations.ClearImage(this.image);

    public unsafe JxlDctAcPointer GetPlaneRow(int channel, int y, int xBase = 0)
    {
        if (sizeof(T) == 4)
        {
            Span<int> span = (this.image as JxlImage3<int>)!.PlaneRow(channel, y)[xBase..];
            return new JxlDctAcPointer() { Pointer32 = span };
        }
        else
        {
            Span<short> span = (this.image as JxlImage3<short>)!.PlaneRow(channel, y)[xBase..];
            return new JxlDctAcPointer() { Pointer16 = span };
        }
    }

    public unsafe JxlDctReadOnlyAcPointer GetReadOnlyPlaneRow(int channel, int y, int xBase = 0)
    {
        if (sizeof(T) == 4)
        {
            ReadOnlySpan<int> span = (this.image as JxlImage3<int>)!.PlaneRow(channel, y)[xBase..];
            return new JxlDctReadOnlyAcPointer(span);
        }
        else
        {
            ReadOnlySpan<short> span = (this.image as JxlImage3<short>)!.PlaneRow(channel, y)[xBase..];
            return new JxlDctReadOnlyAcPointer(span);
        }
    }

    public void Dispose()
    {
        this.image.Dispose();
        GC.SuppressFinalize(this);
    }
}
