// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

/// <summary>
/// Provides methods for processing 2D views of memory used by the
/// JPEG XL codec.
/// </summary>
internal static class JxlImageOperations
{
    /// <summary>
    /// Returns true if first image has same width and height as the second image.
    /// </summary>
    /// <typeparam name="TFrom">The sample type of the first plane.</typeparam>
    /// <typeparam name="TTo">The sample type of the second plane.</typeparam>
    /// <param name="a">First image</param>
    /// <param name="b">Second image</param>
    /// <returns>True if width and height is equal.</returns>
    public static bool SameSize<TFrom, TTo>(JxlPlane<TFrom> a, JxlPlane<TTo> b)
        where TFrom : unmanaged
        where TTo : unmanaged => a.XSize == b.XSize && a.YSize == b.YSize;

    /// <summary>
    /// Returns true if first image has same width and height as the second image.
    /// </summary>
    /// <param name="a">First image</param>
    /// <param name="b">Second image</param>
    /// <returns>True if width and height is equal.</returns>
    public static bool SameSize<T>(JxlImage3<T> a, JxlImage3<T> b)
        where T : unmanaged => a.XSize == b.XSize && a.YSize == b.YSize;

    /// <summary>
    /// Copies logical samples between equally sized planes without changing row padding or a storage prefix.
    /// </summary>
    /// <typeparam name="T">The type of planes to copy.</typeparam>
    /// <param name="from">Source plane (read-only)</param>
    /// <param name="to">Destination plane (write-only)</param>
    /// <returns>
    /// Status of the copy operation
    /// </returns>
    public static bool CopyImage<T>(JxlPlane<T> from, JxlPlane<T> to)
        where T : unmanaged
    {
        if (!SameSize(from, to))
        {
            return false;
        }

        if (from.XSize == 0 || from.YSize == 0)
        {
            return true;
        }

        // Borrow each allocation once at its logical origin. Source and destination can retain different physical row strides.
        ReadOnlySpan<T> source = from.GetRow(0);
        Span<T> destination = to.GetRow(0);

        for (int y = 0; y < from.YSize; y++)
        {
            source.Slice(y * from.PixelsPerRow, from.XSize).CopyTo(destination.Slice(y * to.PixelsPerRow, to.XSize));
        }

        return true;
    }

    /// <summary>
    /// Copies everything from one plane to another. If the copy
    /// fails, an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The type of planes to copy.</typeparam>
    /// <param name="from">Source plane (read-only)</param>
    /// <param name="to">Destination plane (write-only)</param>
    public static void ForceCopyImage<T>(JxlPlane<T> from, JxlPlane<T> to)
        where T : unmanaged
    {
        if (!CopyImage(from, to))
        {
            throw new InvalidOperationException("Could not copy an image");
        }
    }

    /// <summary>
    /// Copies logical samples in all three planes without changing row padding or a storage prefix.
    /// </summary>
    /// <typeparam name="T">The type of image to copy.</typeparam>
    /// <param name="from">Source image (read-only)</param>
    /// <param name="to">Destination image (write-only)</param>
    /// <returns>
    /// Status of the copy operation
    /// </returns>
    public static bool CopyImage<T>(JxlImage3<T> from, JxlImage3<T> to)
        where T : unmanaged
    {
        if (!SameSize(from, to))
        {
            return false;
        }

        if (from.XSize == 0 || from.YSize == 0)
        {
            return true;
        }

        return CopyImage(from.Plane(0), to.Plane(0))
            && CopyImage(from.Plane(1), to.Plane(1))
            && CopyImage(from.Plane(2), to.Plane(2));
    }

    /// <summary>
    /// Copies everything from one image to another. If the copy
    /// fails, an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The type of image to copy.</typeparam>
    /// <param name="from">Source image (read-only)</param>
    /// <param name="to">Destination image (write-only)</param>
    public static void ForceCopyImage<T>(JxlImage3<T> from, JxlImage3<T> to)
        where T : unmanaged
    {
        if (!CopyImage(from, to))
        {
            throw new InvalidOperationException("Could not copy an image");
        }
    }

    /// <summary>
    /// Within bounds specified by input rectangles, copies everything from one plane to another.
    /// </summary>
    /// <typeparam name="T">The type of planes to copy.</typeparam>
    /// <param name="rectFrom">Area in the source plane.</param>
    /// <param name="from">Source plane to copy (read-only)</param>
    /// <param name="rectTo">Area in the destination plane.</param>
    /// <param name="to">Destination plane to copy (write-only)</param>
    /// <returns>
    /// Status of the copy operation.
    /// </returns>
    /// <remarks>
    /// Rectangles MUST be within plane bounds and both should be same.
    /// </remarks>
    public static bool CopyImageTo<T>(Rectangle rectFrom, JxlPlane<T> from, Rectangle rectTo, JxlPlane<T> to)
        where T : unmanaged
    {
        if (rectFrom.Size != rectTo.Size)
        {
            return false;
        }

        if (!from.IsRectangleInside(rectFrom))
        {
            return false;
        }

        if (!to.IsRectangleInside(rectTo))
        {
            return false;
        }

        if (rectFrom.Width == 0)
        {
            return true;
        }

        for (int y = 0; y < rectFrom.Height; y++)
        {
            Span<T> rowFrom = from.GetRow(rectFrom, y);
            Span<T> rowTo = to.GetRow(rectTo, y);
            rowFrom[..rectFrom.Width].CopyTo(rowTo);
        }

        return true;
    }

    /// <summary>
    /// Within bounds specified by input rectangles, copies everything within every plane
    /// from one image to another.
    /// </summary>
    /// <typeparam name="T">The type of image to copy.</typeparam>
    /// <param name="rectFrom">Area in the source image.</param>
    /// <param name="from">Source image to copy (read-only)</param>
    /// <param name="rectTo">Area in the destination image.</param>
    /// <param name="to">Destination image to copy (write-only)</param>
    /// <returns>
    /// Status of the copy operation.
    /// </returns>
    /// <remarks>
    /// Rectangles MUST be within image bounds and both should be same.
    /// </remarks>
    public static bool CopyImageTo<T>(Rectangle rectFrom, JxlImage3<T> from, Rectangle rectTo, JxlImage3<T> to)
        where T : unmanaged
    {
        if (rectFrom.Size != rectTo.Size)
        {
            return false;
        }

        for (int plane = 0; plane < 3; plane++)
        {
            if (!CopyImageTo(rectFrom, from.Plane(plane), rectTo, to.Plane(plane)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts a plane from one type to another within specified bounds and ensures to clamp values if the
    /// minimum and maximum limits of the result type are lower than the input type (e.g., input is
    /// <see cref="int"/> and destination is <see cref="byte"/>).
    /// </summary>
    /// <typeparam name="TFrom">Type of the source plane</typeparam>
    /// <typeparam name="TTo">Type of the destination plane</typeparam>
    /// <param name="rectFrom">The area of the source plane</param>
    /// <param name="from">The source plane (read-only)</param>
    /// <param name="rectTo">The area of the destination plane</param>
    /// <param name="to">The destination plane (write-only)</param>
    /// <returns>
    /// Status of the copy operation.
    /// </returns>
    public static bool ConvertPlaneAndClamp<TFrom, TTo>(Rectangle rectFrom, JxlPlane<TFrom> from, Rectangle rectTo, JxlPlane<TTo> to)
        where TFrom : unmanaged, INumber<TFrom>
        where TTo : unmanaged, INumber<TTo>
    {
        if (rectFrom.Size != rectTo.Size)
        {
            return false;
        }

        for (int y = 0; y < rectTo.Height; y++)
        {
            Span<TFrom> rowFrom = from.GetRow(rectFrom, y);
            Span<TTo> rowTo = to.GetRow(rectTo, y);

            for (int x = 0; x < rectTo.Width; x++)
            {
                rowTo[x] = TTo.CreateSaturating(rowFrom[x]);
            }
        }

        return true;
    }

    /// <summary>
    /// <para>
    ///   Copies an image region from <paramref name="from"/> to <paramref name="to"/>,
    ///   including up to <paramref name="padding"/> pixels of surrounding source data
    ///   on each side of the region.
    /// </para>
    /// <para>
    ///   Padding is taken from the neighboring pixels in the source plane and is
    ///   limited by the source plane boundaries. The destination rectangle is
    ///   expanded by the same amount to preserve the relative pixel positions.
    ///   Returns <see langword="false"/> if the destination does not have enough
    ///   space for the required left or top padding.
    /// </para>
    /// </summary>
    /// <param name="fromRect">The source plane area</param>
    /// <param name="from">The source plane (read-only)</param>
    /// <param name="padding">The maximum number of pixels to include around the source region.</param>
    /// <param name="toRect">The destination plane area.</param>
    /// <param name="to">The destination place (write-only)</param>
    /// <returns>
    /// <see langword="true"/> if the region was copied successfully;
    /// otherwise, <see langword="false"/> if the destination cannot accommodate
    /// the required padding.
    /// </returns>
    public static bool CopyImageToWithPadding<T>(Rectangle fromRect, JxlPlane<T> from, int padding, Rectangle toRect, JxlPlane<T> to)
        where T : unmanaged
    {
        int xExtra0 = Math.Min(padding, fromRect.X);
        int xExtra1 = Math.Min(
            padding,
            from.XSize - fromRect.X - fromRect.Width);

        int yExtra0 = Math.Min(padding, fromRect.Y);
        int yExtra1 = Math.Min(
            padding,
            from.YSize - fromRect.Y - fromRect.Height);

        if (toRect.X < xExtra0 || toRect.Y < yExtra0)
        {
            return false;
        }

        return CopyImageTo(
            new Rectangle(
                fromRect.X - xExtra0,
                fromRect.Y - yExtra0,
                fromRect.Width + xExtra0 + xExtra1,
                fromRect.Height + yExtra0 + yExtra1),
            from,
            new Rectangle(
                toRect.X - xExtra0,
                toRect.Y - yExtra0,
                toRect.Width + xExtra0 + xExtra1,
                toRect.Height + yExtra0 + yExtra1),
            to);
    }

    /// <summary>
    /// Performs linear combination of two grayscale images, and allocates &amp; returns the
    /// image with the linear combination. The returned image can later be disposed.
    /// </summary>
    /// <typeparam name="T">The type of the plane. This type should be numeric as linear combination involves multiplication.</typeparam>
    /// <param name="configuration">The configuration which includes a memory allocator for the return value.</param>
    /// <param name="lambda1">The lambda for image 1.</param>
    /// <param name="image1">The first image.</param>
    /// <param name="lambda2">The lambda for image 2.</param>
    /// <param name="image2">The second image.</param>
    /// <returns>A new image with linear combination, or null if it failed.</returns>
    public static JxlPlane<T>? LinComb<T>(Configuration configuration, T lambda1, JxlPlane<T> image1, T lambda2, JxlPlane<T> image2)
        where T : unmanaged, INumber<T>
    {
        int xSize = image1.XSize;
        int ySize = image1.YSize;

        if (xSize != image2.XSize || ySize != image2.YSize)
        {
            return null;
        }

        JxlPlane<T> result = JxlPlane<T>.Create(configuration, xSize, ySize);

        for (int y = 0; y < ySize; y++)
        {
            Span<T> row1 = image1.GetRow(y);
            Span<T> row2 = image2.GetRow(y);
            Span<T> rowOut = result.GetRow(y);

            for (int x = 0; x < xSize; x++)
            {
                rowOut[x] = (lambda1 * row1[x]) + (lambda2 * row2[x]);
            }
        }

        return result;
    }

    /// <summary>
    /// Multiplies all image values by the lambda in-place.
    /// </summary>
    /// <typeparam name="T">The type of the plane. Should be numeric.</typeparam>
    /// <param name="lambda">The lambda for multiplication.</param>
    /// <param name="image">The image to multiply.</param>
    public static void ScaleImage<T>(T lambda, JxlPlane<T> image)
        where T : unmanaged, INumber<T>
    {
        for (int y = 0; y < image.YSize; y++)
        {
            // TODO: SIMD
            Span<T> row = image.GetRow(y);
            for (int x = 0; x < image.XSize; x++)
            {
                row[x] = lambda * row[x];
            }
        }
    }

    /// <summary>
    /// Multiplies all image values within all planes by the lambda in-place.
    /// </summary>
    /// <typeparam name="T">The type of the image. Should be numeric.</typeparam>
    /// <param name="lambda">The lambda for multiplication.</param>
    /// <param name="image">The image to multiply.</param>
    public static void ScaleImage<T>(T lambda, JxlImage3<T> image)
        where T : unmanaged, INumber<T>
    {
        for (int plane = 0; plane < 3; plane++)
        {
            ScaleImage(lambda, image.Plane(plane));
        }
    }

    /// <summary>
    /// Fills logical samples with <paramref name="value"/> without changing row padding or a storage prefix.
    /// </summary>
    /// <typeparam name="T">The type of the plane.</typeparam>
    /// <param name="value">The value to fill everything with.</param>
    /// <param name="image">The plane to fill.</param>
    public static void FillImage<T>(T value, JxlPlane<T> image)
        where T : unmanaged
    {
        // GetRow(0) excludes any prefix. Slice the retained stride so padding remains unchanged after a logical shrink.
        Span<T> storage = image.GetRow(0);

        for (int y = 0; y < image.YSize; y++)
        {
            storage.Slice(y * image.PixelsPerRow, image.XSize).Fill(value);
        }
    }

    /// <summary>
    /// Fills logical samples in all three planes with <paramref name="value"/>, leaving padding unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the image.</typeparam>
    /// <param name="value">The value to fill every plane with.</param>
    /// <param name="image">The image to fill.</param>
    public static void FillImage<T>(T value, JxlImage3<T> image)
        where T : unmanaged
    {
        FillImage(value, image.Plane(0));
        FillImage(value, image.Plane(1));
        FillImage(value, image.Plane(2));
    }

    /// <summary>
    /// Clears logical samples without changing row padding or a storage prefix.
    /// </summary>
    /// <typeparam name="T">The type of the plane.</typeparam>
    /// <param name="image">The image to clear.</param>
    public static void ZeroFillImage<T>(JxlPlane<T> image)
        where T : unmanaged
    {
        if (image.XSize == 0)
        {
            return;
        }

        Span<T> storage = image.GetRow(0);

        // Only the logical width belongs to this clear. Retained capacity can include padding and unused rows.
        for (int y = 0; y < image.YSize; y++)
        {
            storage.Slice(y * image.PixelsPerRow, image.XSize).Clear();
        }
    }

    /// <summary>
    /// Core method for the WrapMirror function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mirror(long x, long xSize)
    {
        DebugGuard.MustBeGreaterThan(xSize, 0, nameof(xSize));

        while (true)
        {
            if (x < 0)
            {
                x = -x - 1;
            }
            else if (x >= xSize)
            {
                x = (2 * xSize) - 1 - x;
            }
            else
            {
                break;
            }
        }

        return (int)x;
    }

    /// <summary>
    /// Finds the smallest and largest logical samples without reading a storage prefix, row padding or unused rows.
    /// </summary>
    /// <typeparam name="T">The type of the plane.</typeparam>
    /// <param name="image">The plane where to search for the minimum &amp; maximum values.</param>
    /// <param name="min">Lowest value found in the plane.</param>
    /// <param name="max">Largest value found in the plane.</param>
    /// <remarks>An empty plane returns the sample type's maximum value as min and minimum value as max.</remarks>
    public static void ImageMinMax<T>(JxlPlane<T> image, out T min, out T max)
        where T : unmanaged, INumber<T>, IMinMaxValue<T>
    {
        min = T.MaxValue;
        max = T.MinValue;

        // Empty planes are valid owners. Retain the existing empty result without passing an empty row to tensor reductions.
        if (image.XSize == 0)
        {
            return;
        }

        ReadOnlySpan<T> samples = image.GetRow(0);

        for (int y = 0; y < image.YSize; y++)
        {
            // Reduce only this logical row. Shared tensor operations supply all supported SIMD widths and scalar remainders.
            ReadOnlySpan<T> row = samples.Slice(y * image.PixelsPerRow, image.XSize);
            min = T.Min(min, TensorPrimitives.Min(row));
            max = T.Max(max, TensorPrimitives.Max(row));
        }
    }

    /// <summary>
    /// Within the bounds specified by the rectangle <paramref name="rect"/>, sets every
    /// value within the area to be <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The type of the plane.</typeparam>
    /// <param name="value">The value to fill the area with.</param>
    /// <param name="image">The input plane.</param>
    /// <param name="rect">The area of the plane to fill everything with.</param>
    public static void FillPlane<T>(T value, JxlPlane<T> image, Rectangle rect)
        where T : unmanaged
    {
        for (int y = 0; y < rect.Height; y++)
        {
            Span<T> row = image.GetRow(rect, y);
            row[..rect.Width].Fill(value);
        }
    }

    /// <summary>
    /// Clears logical samples in all three planes, leaving padding unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the image</typeparam>
    /// <param name="image">The image whose all planes will be cleared.</param>
    public static void ZeroFillImage<T>(JxlImage3<T> image)
        where T : unmanaged
    {
        ZeroFillImage(image.Plane(0));
        ZeroFillImage(image.Plane(1));
        ZeroFillImage(image.Plane(2));
    }

    private static bool DownsampleImageCore(JxlPlane<float> input, int factor, JxlPlane<float> output)
    {
        if (factor == 1)
        {
            return false;
        }

        if (!output.ShrinkTo(JxlMath.DivCeil(input.XSize, factor), JxlMath.DivCeil(input.YSize, factor)))
        {
            return false;
        }

        int inStride = input.PixelsPerRow;
        for (int y = 0; y < output.YSize; y++)
        {
            Span<float> rowOut = output.GetRow(y);
            Span<float> rowIn = input.GetRow(factor * y);
            for (int x = 0; x < output.XSize; x++)
            {
                int count = 0;
                float sum = 0;
                for (int iy = 0; iy < factor && iy + (factor * y) < input.YSize; iy++)
                {
                    for (int ix = 0; ix < factor && ix + (factor * x) < input.XSize; ix++)
                    {
                        sum += rowIn[(iy * inStride) + (x * factor) + ix];
                        count++;
                    }
                }

                rowOut[x] = sum / count;
            }
        }

        return true;
    }

    /// <summary>
    /// Downsamples the image. The resulting image can later be disposed.
    /// </summary>
    /// <param name="configuration">Configuration which has a memory allocator which is used to allocate the result image.</param>
    /// <param name="image">The image to downsample.</param>
    /// <param name="factor">Downsampling factor.</param>
    /// <returns>A new downsampled image. It can later be disposed.</returns>
    public static JxlPlane<float>? DownsampleImage(Configuration configuration, JxlPlane<float> image, int factor)
    {
        JxlPlane<float> downsampled = new(
            configuration,
            JxlMath.DivCeil(image.XSize, factor) + JxlFrameDimensions.BlockDimensions,
            JxlMath.DivCeil(image.YSize, factor) + JxlFrameDimensions.BlockDimensions);

        if (!DownsampleImageCore(image, factor, downsampled))
        {
            downsampled.Dispose();
            return null;
        }

        return downsampled;
    }

    /// <summary>
    /// Downsamples all planes within the image. The resulting image can later be disposed.
    /// </summary>
    /// <param name="configuration">Configuration which has a memory allocator which is used to allocate the result image.</param>
    /// <param name="opsin">The image to downsample.</param>
    /// <param name="factor">Downsampling factor.</param>
    /// <returns>A new downsampled image. It can later be disposed.</returns>
    public static JxlImage3<float>? DownsampleImage(Configuration configuration, JxlImage3<float> opsin, int factor)
    {
        if (factor == 1)
        {
            return null;
        }

        JxlImage3<float> downsampled = new(
            configuration,
            JxlMath.DivCeil(opsin.XSize, factor) + JxlFrameDimensions.BlockDimensions,
            JxlMath.DivCeil(opsin.YSize, factor) + JxlFrameDimensions.BlockDimensions);

        if (!downsampled.ShrinkTo(
            downsampled.XSize - JxlFrameDimensions.BlockDimensions,
            downsampled.YSize - JxlFrameDimensions.BlockDimensions))
        {
            return null;
        }

        for (int plane = 0; plane < 3; plane++)
        {
            if (!DownsampleImageCore(opsin.Plane(plane), factor, downsampled.Plane(plane)))
            {
                downsampled.Dispose();
                return null;
            }
        }

        return downsampled;
    }

    public static bool PadImageToBlockMultipleInPlace(JxlImage3<float> input, int blockDimensions)
    {
        int xSizeOriginal = input.XSize;
        int ySizeOriginal = input.YSize;

        int xSize = JxlMath.RoundUpTo(xSizeOriginal, blockDimensions);
        int ySize = JxlMath.RoundUpTo(ySizeOriginal, blockDimensions);

        if (!input.ShrinkTo(xSize, ySize))
        {
            return false;
        }

        for (int plane = 0; plane < 3; plane++)
        {
            for (int y = 0; y < ySizeOriginal; y++)
            {
                Span<float> row = input.PlaneRow(plane, y);
                row.Slice(xSizeOriginal, xSize - xSizeOriginal).Fill(row[xSizeOriginal - 1]);
            }

            Span<float> sourceRow = input.PlaneRow(plane, ySizeOriginal - 1)[..xSize];
            for (int y = ySizeOriginal; y < ySize; y++)
            {
                sourceRow.CopyTo(input.PlaneRow(plane, y));
            }
        }

        return true;
    }
}
