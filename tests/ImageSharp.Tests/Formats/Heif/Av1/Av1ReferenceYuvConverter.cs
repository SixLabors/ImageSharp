// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// This simulates converting 24-bit RGB values to YUV, then back to 24-bit RGB.
/// Using BT.709 transfer functions: https://en.wikipedia.org/wiki/Rec._709
///
/// It demonstrates that converting to 30-bit YUV then back to 24-bit RGB is lossy.
/// Using 10 bits per YUV value appears to be lossless.
///
/// Converting RGB (24-bit) -> YUV(64-bit floats per channel, normalized[0 - 1]) -> RGB(24-bit)
/// Found 0 inaccurate conversions out of 16581375 RGB values
///
/// Converting RGB(24-bit) -> YUV(30-bit) -> RGB(24-bit)
/// Found 0 inaccurate conversions out of 16581375 RGB values
///
/// Converting RGB(24-bit) -> YUV(24-bit) -> RGB(24-bit)
/// Found 4058422 accurate conversions out of 16581375 RGB values
/// Found 12522953 inaccurate conversions out of 16581375 RGB values
/// Off by: {1: 8786792, 2: 3727753, 3: 8408}
/// </summary>
/// <remarks> Ported from Python to C# from: https://gist.github.com/linrock/5be4f365c9c9e61eee9e8984ba13cb25.</remarks>
internal class Av1ReferenceYuvConverter
{
    // The range of UV values in BT.709 is [-Umax, Umax] and [-Vmax, Vmax]
    private const double Umax = 0.436;
    private const double Vmax = 0.615;

    // Constants used in BT.709
    private const double Wr = 0.2126;
    private const double Wb = 0.0722;

    // Constants used in BT.601
    // private const double Wr = 0.299;
    // private const double Wb = 0.114;

    private const double Wg = 1 - Wr - Wb;

    public static Span<Rgb24> RgbToYuv(Span<Rgb24> row)
    {
        Rgb24[] result = new Rgb24[row.Length];
        for (int i = 0; i < row.Length; i++)
        {
            double[] current = RgbToYuv(row[i], false, true, false);
            byte y = (byte)current[0];
            byte u = (byte)current[1];
            byte v = (byte)current[2];
            result[i] = new Rgb24(y, u, v);
        }

        return result;
    }

    public static double[] RgbToYuv(Rgb24 rgb, bool normalize = false, bool is_8bit = false, bool is_10bit = false)
    {
        double r = rgb.R / 255.0;
        double g = rgb.G / 255.0;
        double b = rgb.B / 255.0;
        double y = (Wr * r) + (Wg * g) + (Wb * b);
        double u = Umax * (b - y) / (1 - Wb);
        double v = Vmax * (r - y) / (1 - Wr);

        // y[0, 1]  u[-Umax, Umax]  v[-Vmax, Vmax]
        if (normalize)
        {
            u = (u + Umax) / (2 * Umax);
            v = (v + Vmax) / (2 * Vmax);

            // y[0, 1]  u[0, 1]  v[0, 1]
        }

        if (is_8bit)
        {
            y = Math.Round(y * 255);
            u = Math.Round(u * 255);
            v = Math.Round(v * 255);

            // y[0, 255]  u[0, 255]  v[0, 255]
        }

        if (is_10bit)
        {
            y = Math.Round(y * 1023);
            u = Math.Round(u * 1023);
            v = Math.Round(v * 1023);

            // y[0, 1023]  u[0, 1023]  v[0, 1023]
        }

        return [y, u, v];
    }

    public static Span<Rgb24> YuvToRgb(Av1FrameBuffer<byte> frameBuffer)
    {
        Span<byte> yRow = frameBuffer.BufferY!.DangerousGetSingleSpan();
        Span<byte> uRow = frameBuffer.BufferCb!.DangerousGetSingleSpan();
        Span<byte> vRow = frameBuffer.BufferCr!.DangerousGetSingleSpan();
        Rgb24[] result = new Rgb24[yRow.Length];
        double[] yuv = new double[3];
        for (int i = 0; i < yRow.Length; i++)
        {
            yuv[0] = yRow[i];
            yuv[1] = uRow[i];
            yuv[2] = vRow[i];
            result[i] = YuvToRgb(yuv, false, true, false);
        }

        return result;
    }

    public static Rgb24 YuvToRgb(double[] yuv, bool normalized = false, bool is_8bit = false, bool is_10bit = false)
    {
        double y = yuv[0];
        double u = yuv[1];
        double v = yuv[2];
        if (is_8bit)
        {
            // y[0, 255]  u[0, 255]  v[0, 255]
            y /= 255.0;
            u /= 255.0;
            v /= 255.0;
        }

        if (is_10bit)
        {
            // y[0, 1023]  u[0, 1023]  v[0, 1023]
            y /= 1023.0;
            u /= 1023.0;
            v /= 1023.0;
        }

        if (normalized)
        {
            // y [0, 1], u [0, 1], v[0, 1]
            u = (u - 0.5) * 2 * Umax;
            v = (v - 0.5) * 2 * Vmax;

            // y [0, 1], u [-Umax, Umax], v[-Vmax, Vmax]
        }

        // r = y                 + 1.28033 * v
        // g = y  - 0.21482 * u  - 0.38059 * v
        // b = y  + 2.12798 * u
        double r = y + (v * (1 - Wr) / Vmax);
        double g = y - (u * Wb * (1 - Wb) / (Umax * Wg)) - (v * Wr * (1 - Wr) / (Vmax * Wg));
        double b = y + (u * (1 - Wb) / Umax);

        return new Rgb24((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}
