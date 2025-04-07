// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1YuvConverter
{
    // BT.709 SPecificatiuon constants.
    private const int UMax = (int)(0.436 * 255);
    private const int VMax = (int)(0.615 * 255);
    private const int Wr = (int)(0.2126 * 255);
    private const int Wb = (int)(0.0722 * 255);
    private const int Wg = 255 - Wr - Wb;

    public static void ConvertToRgb<TPixel>(Configuration configuration, Av1FrameBuffer<byte> frameBuffer, ImageFrame<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<Rgb24> rgbImage = new(image.Width, image.Height);
        ImageFrame<Rgb24> rgbFrame = rgbImage.Frames.RootFrame;

        // TODO: Support YUV420 and YUV420 also.
        if (frameBuffer.ColorFormat != Av1ColorFormat.Yuv444)
        {
            throw new NotSupportedException("Only able to convert YUV444 to RGB.");
        }

        ConvertYuvToRgb(frameBuffer, rgbFrame, false);
        image.ProcessPixelRows(rgbFrame, (resultAcc, rgbAcc) =>
        {
            for (int y = 0; y < rgbImage.Height; y++)
            {
                Span<Rgb24> rgbRow = rgbAcc.GetRowSpan(y);
                Span<TPixel> resultRow = resultAcc.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromRgb24(configuration, rgbRow, resultRow);
            }
        });
    }

    public static void ConvertFromRgb<TPixel>(Configuration configuration, ImageFrame<TPixel> image, Av1FrameBuffer<byte> frameBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<Rgb24> rgbImage = new(image.Width, image.Height);
        ImageFrame<Rgb24> rgbFrame = rgbImage.Frames.RootFrame;

        image.ProcessPixelRows(rgbFrame, (sourceAcc, rgbAcc) =>
        {
            for (int y = 0; y < rgbImage.Height; y++)
            {
                Span<Rgb24> rgbRow = rgbAcc.GetRowSpan(y);
                Span<TPixel> sourceRow = sourceAcc.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24(configuration, sourceRow, rgbRow);
            }
        });

        // TODO: Support YUV422 and YUV420 also.
        ConvertRgbToYuv444(rgbFrame, frameBuffer);
    }

    private static void ConvertYuvToRgb(Av1FrameBuffer<byte> buffer, ImageFrame<Rgb24> image, bool isSubsampled)
    {
        // Weight multiplied by 256 to exploit full byte resolution, rounded to the nearest integer.
        // Using BT.709 specification
        const int rvWeight = (int)(1.28033 * 255);
        const int guWeight = (int)(-0.21482 * 255);
        const int gvWeight = (int)(-0.38059 * 255);
        const int buWeight = (int)(2.12798 * 255);
        Guard.NotNull(buffer.BufferY);
        Guard.NotNull(buffer.BufferCb);
        Guard.NotNull(buffer.BufferCr);
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferY.Width, image.Width, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferY.Height, image.Height, nameof(buffer));
        if (isSubsampled)
        {
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Width, image.Width >> 1, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Height, image.Height >> 1, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Width, image.Width >> 1, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Height, image.Height >> 1, nameof(buffer));
        }
        else
        {
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Width, image.Width, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Height, image.Height, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Width, image.Width, nameof(buffer));
            Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Height, image.Height, nameof(buffer));
        }

        image.ProcessPixelRows(accessor =>
            {
                Span<byte> yBuffer = buffer.DeriveBlockPointer(Av1Plane.Y, default, 0, 0, out int yStride);
                Span<byte> uBuffer = buffer.DeriveBlockPointer(Av1Plane.U, default, 0, 0, out int uStride);
                Span<byte> vBuffer = buffer.DeriveBlockPointer(Av1Plane.V, default, 0, 0, out int vStride);
                int yOffset = yStride;
                int uOffset = uStride;
                int vOffset = vStride;
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgb24> rgbRow = accessor.GetRowSpan(y);
                    ref Rgb24 pixel = ref rgbRow[0];
                    ref byte yRef = ref yBuffer[yOffset];
                    ref byte uRef = ref uBuffer[uOffset];
                    ref byte vRef = ref vBuffer[vOffset];
                    for (int x = 0; x < image.Width; x++)
                    {
                        int u = uRef; // ((uRef - 127) * 2 * UMax) / 255;
                        int v = vRef; // ((vRef - 127) * 2 * VMax) / 255;
                        pixel.R = (byte)Av1Math.Clip3(0, 255, yRef + (v * (255 - Wr) / VMax));
                        pixel.G = (byte)Av1Math.Clip3(0, 255, yRef - ((u * Wb * (255 - Wb)) / (UMax * Wg)) - ((v * Wr * (255 - Wr)) / (VMax * Wg)));
                        pixel.B = (byte)Av1Math.Clip3(0, 255, yRef + ((u * (255 - Wb)) / UMax));
                        pixel = ref Unsafe.Add(ref pixel, 1);
                        yRef = ref Unsafe.Add(ref yRef, 1);
                        uRef = ref Unsafe.Add(ref uRef, 1);
                        vRef = ref Unsafe.Add(ref vRef, 1);
                    }

                    yOffset += yStride;
                    uOffset += uStride;
                    vOffset += vStride;
                }
            });
    }

    private static void ConvertRgbToYuv444(ImageFrame<Rgb24> image, Av1FrameBuffer<byte> buffer)
    {
        // Weight multiplied by 256 to exploit full byte resolution, rounded to the nearest integer.
        const int yrWeight = (int)(0.2126 * 255);
        const int ygWeight = (int)(0.7152 * 255);
        const int ybWeight = (int)(0.0722 * 255);
        const int urWeight = (int)(-0.09991 * 255);
        const int ugWeight = (int)(-0.33609 * 255);
        const int ubWeight = (int)(0.436 * 255);
        const int vrWeight = (int)(0.615 * 255);
        const int vgWeight = (int)(-0.55861 * 255);
        const int vbWeight = (int)(-0.05639 * 255);
        Guard.NotNull(buffer.BufferY);
        Guard.NotNull(buffer.BufferCb);
        Guard.NotNull(buffer.BufferCr);
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferY.Width, image.Width, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferY.Height, image.Height, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Width, image.Width, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCb.Height, image.Height, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Width, image.Width, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(buffer.BufferCr.Height, image.Height, nameof(buffer));

        image.ProcessPixelRows(accessor =>
        {
            Buffer2D<byte> yBuffer = buffer.BufferY;
            Buffer2D<byte> uBuffer = buffer.BufferCb;
            Buffer2D<byte> vBuffer = buffer.BufferCr;
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgb24> rgbRow = accessor.GetRowSpan(y);
                ref Rgb24 pixel = ref rgbRow[0];
                Span<byte> ySpan = yBuffer.DangerousGetRowSpan(y);
                ref byte yRef = ref ySpan[0];
                Span<byte> uSpan = uBuffer.DangerousGetRowSpan(y);
                ref byte uRef = ref uSpan[0];
                Span<byte> vSpan = vBuffer.DangerousGetRowSpan(y);
                ref byte vRef = ref vSpan[0];
                for (int x = 0; x < image.Width; x++)
                {
                    yRef = (byte)Av1Math.Clip3(0, 255, ((Wr * pixel.R) + (Wg * pixel.G) + (Wb * pixel.B)) / 255);

                    // Not normalized, where range is [-UMax, UMax] or [-VMax, VMax]
                    // uRef = (byte)((UMax * (pixel.B - y)) / (255 - Wb));
                    // vRef = (byte)((VMax * (pixel.R - y)) / (255 - Wr));

                    // Normalized calculations
                    uRef = (byte)Av1Math.Clip3(0, 255, ((UMax * (pixel.B - yRef) / (255 - Wb)) + UMax) * 255 / (2 * UMax));
                    vRef = (byte)Av1Math.Clip3(0, 255, ((VMax * (pixel.R - yRef) / (255 - Wr)) + VMax) * 255 / (2 * VMax));
                    pixel = ref Unsafe.Add(ref pixel, 1);
                    yRef = ref Unsafe.Add(ref yRef, 1);
                    uRef = ref Unsafe.Add(ref uRef, 1);
                    vRef = ref Unsafe.Add(ref vRef, 1);
                }
            }
        });
    }
}
