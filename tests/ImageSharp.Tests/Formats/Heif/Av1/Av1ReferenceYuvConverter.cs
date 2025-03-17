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

    public static Span<Rgb24> RgbToYuv(Span<Rgb24> row, bool normalized)
    {
        Rgb24[] result = new Rgb24[row.Length];
        for (int i = 0; i < row.Length; i++)
        {
            double[] current = RgbToYuv(row[i], normalized, true, false);
            double y = Math.Max(0, Math.Min(255, Math.Round(current[0])));
            double u = Math.Max(0, Math.Min(255, Math.Round(current[1])));
            double v = Math.Max(0, Math.Min(255, Math.Round(current[2])));

            result[i] = new Rgb24((byte)y, (byte)u, (byte)v);
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

    public static Span<Rgb24> YuvToRgb(Av1FrameBuffer<byte> frameBuffer, bool normalized)
    {
        Point pixelPosition = new Point(0, 1);
        Span<byte> yRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, pixelPosition, 0, 0, out int _);
        Span<byte> uRow = frameBuffer.DeriveBlockPointer(Av1Plane.U, pixelPosition, 0, 0, out int _);
        Span<byte> vRow = frameBuffer.DeriveBlockPointer(Av1Plane.V, pixelPosition, 0, 0, out int _);
        Rgb24[] result = new Rgb24[frameBuffer.Width];
        double[] yuv = new double[3];
        for (int i = 0; i < frameBuffer.Width; i++)
        {
            yuv[0] = yRow[i];
            yuv[1] = uRow[i];
            yuv[2] = vRow[i];
            double[] rgb = YuvToRgb(yuv, normalized, true, false);
            double r = rgb[0] * 255;
            double g = rgb[1] * 255;
            double b = rgb[2] * 255;
            byte redByte = (byte)Math.Max(0, Math.Min(255, Math.Round(r)));
            byte greenByte = (byte)Math.Max(0, Math.Min(255, Math.Round(g)));
            byte blueByte = (byte)Math.Max(0, Math.Min(255, Math.Round(b)));

            // Assert.True(Math.Abs(redByte - r) < 3, $"Red pixel out of byte range: {redByte} iso {r} from input Y={yuv[0]}, U={yuv[1]} and V={yuv[2]}.");
            // Assert.True(Math.Abs(greenByte - g) < 3, $"Green pixel out of byte range: {greenByte} iso {g} from input Y={yuv[0]}, U={yuv[1]} and V={yuv[2]}.");
            // Assert.True(Math.Abs(blueByte - b) < 3, $"Blue pixel out of byte range: {blueByte} iso {b} from input Y={yuv[0]}, U={yuv[1]} and V={yuv[2]}.");

            result[i] = new Rgb24(redByte, greenByte, blueByte);
        }

        return result;
    }

    public static double[] YuvToRgb(double[] yuv, bool normalized = false, bool is_8bit = false, bool is_10bit = false)
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

        return [r, g, b];
    }
}
