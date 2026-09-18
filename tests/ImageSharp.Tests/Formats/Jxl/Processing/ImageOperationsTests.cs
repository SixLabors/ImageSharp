// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class ImageOperationsTests
{
    private static void TestFillImpl<T>(JxlImage3<T> image, string layout)
        where T : unmanaged
    {
        JxlImageOperations.FillImage((T)Convert.ChangeType(1, typeof(T), null), image);

        for (int y = 0; y < image.YSize; y++)
        {
            for (int c = 0; c < 3; c++)
            {
                Span<T> row = image.PlaneRow(c, y);

                for (int x = 0; x < image.XSize; x++)
                {
                    if (!EqualityComparer<T>.Default.Equals(
                        row[x],
                        (T)Convert.ChangeType(1, typeof(T), null)))
                    {
                        Assert.Fail(
                            $"Not 1 at c={c} {x}, {y} " +
                            $"({image.XSize} x {image.YSize}) ({layout})");
                    }

                    row[x] = (T)Convert.ChangeType(2, typeof(T), null);
                }
            }
        }

        JxlImageOperations.ZeroFillImage(image);

        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < image.YSize; y++)
            {
                Span<T> row = image.PlaneRow(c, y);

                for (int x = 0; x < image.XSize; x++)
                {
                    if (!EqualityComparer<T>.Default.Equals(
                        row[x],
                        default))
                    {
                        Assert.Fail(
                            $"Not 0 at c={c} {x}, {y} " +
                            $"({image.XSize} x {image.YSize}) ({layout})");
                    }

                    row[x] = (T)Convert.ChangeType(3, typeof(T), null);
                }
            }
        }
    }

    private static void TestFillT<T>()
        where T : unmanaged
    {
        foreach (uint xSize in new uint[] { 0, 1, 15, 16, 31, 32 })
        {
            foreach (uint ySize in new uint[] { 0, 1, 15, 16, 31, 32 })
            {
                JxlImage3<T> image = JxlImage3<T>.Create(TestEnvironment.Configuration, (int)xSize, (int)ySize);

                TestFillImpl(image, "size ctor");
            }
        }
    }

    [Fact]
    public void TestFill()
    {
        TestFillT<byte>();
        TestFillT<short>();
        TestFillT<float>();
    }

    [Fact]
    public void CopyImageToWithPaddingTest()
    {
        JxlPlane<uint> src = JxlPlane<uint>.Create(TestEnvironment.Configuration, 100, 61);

        for (int y = 0; y < src.YSize; y++)
        {
            Span<uint> row = src.GetRow(y);

            for (int x = 0; x < src.XSize; x++)
            {
                row[x] = (uint)((x * 1000) + y);
            }
        }

        Rectangle srcRect = new(10, 20, 30, 40);
        Assert.True(srcRect.IsInside(src.GetRectangle()));

        JxlPlane<uint> dst = JxlPlane<uint>.Create(TestEnvironment.Configuration, 60, 50);

        JxlImageOperations.FillImage(0u, dst);

        Rectangle dstRect = new(20, 5, 30, 40);
        Assert.True(dstRect.IsInside(dst.GetRectangle()));

        Assert.True(
            JxlImageOperations.CopyImageToWithPadding(
                srcRect,
                src,
                padding: 2,
                dstRect,
                dst));

        Rectangle paddedDstRect = new(
            20 - 2,
            5 - 2,
            30 + 4,
            40 + 3);

        for (int y = 0; y < dst.YSize; y++)
        {
            Span<uint> row = dst.GetRow(y);

            for (int x = 0; x < dst.XSize; x++)
            {
                if (new Rectangle(x, y, 1, 1).IsInside(paddedDstRect))
                {
                    Assert.Equal(
                        (uint)(
                            ((x - dstRect.X0() + srcRect.X0()) * 1000) +
                            (y - dstRect.Y0() + srcRect.Y0())),
                        row[x]);
                }
                else
                {
                    Assert.Equal(0u, row[x]);
                }
            }
        }
    }
}
