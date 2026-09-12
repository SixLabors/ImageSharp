// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

/// <summary>
/// Hybrid Log Gamma transfer function.
/// </summary>
internal sealed class JxlHlgTransferFunction
{
    // Shared constants used by transfer functions
    private const float A = 0.17883277f;
    private const float RA = 1.0f / A;
    private const float B = 1 - (4 * A);
    private const float C = 0.5599107295f;
    private const float Inverse12 = 1.0f / 12.0f;
    private const float HiAdd = B * Inverse12;
    private const float HiMul = 0.003639807079052639f; // MathF.Exp(-C * RA) * Inverse12
    private const float HiPow = 8.067285659607931f; // RA * JxlMath.InverseLog2E

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlHlgTransferFunction"/> class.
    /// </summary>
    /// <remarks>
    /// Use static methods. Don't instantiate this class.
    /// </remarks>
    private JxlHlgTransferFunction()
    {
    }

    public static Vector<float> EncodedFromDisplay(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);
        Vector<int> belowInverse12 = Vector.LessThan(x, Vector.Create(Inverse12));

        Vector<float> lo = Vector.SquareRoot(Vector.Create(3.0f) * x);
        Vector<float> hi = (Vector.Create(A * JxlMath.InverseLog2E) * Vector.Log2((Vector.Create(12f) * x) + Vector.Create(-B))) + Vector.Create(C);
        Vector<float> magnitude = Vector.ConditionalSelect(belowInverse12, lo, hi);
        return Vector.AndNot(sign, magnitude) | originalSign;
    }

    public static Vector<float> DisplayFromEncoded(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);
        Vector<int> below05 = Vector.LessThan(x, Vector.Create(0.5f));

        Vector<float> lo = x * (x * Vector.Create(1f / 3f));
        Vector<float> hi = (Pow2(x * Vector.Create(HiPow)) * Vector.Create(HiMul)) + Vector.Create(HiAdd);
        Vector<float> magnitude = Vector.ConditionalSelect(below05, lo, hi);

        return Vector.AndNot(sign, magnitude) | originalSign;
    }

    private static Vector<float> Pow2(Vector<float> x) => JxlSimdUtils.FastPow2f(x);

    /// <summary>
    /// Converts encoded signal to display signal.
    /// </summary>
    /// <param name="encoded">The encoded signal</param>
    /// <returns>The display signal</returns>
    public static double DisplayFromEncoded(double encoded) => Ootf(InverseOotf(encoded));

    /// <summary>
    /// Converts display signal to encoded signal.
    /// </summary>
    /// <param name="display">The display signal</param>
    /// <returns>The encoded signal</returns>
    public static double EncodedFromDisplay(double display) => Oetf(InverseOetf(display));

    /// <summary>
    /// Opto-Electronic Transfer Function - converts
    /// real-world scene light (s) into a digital video
    /// signal inside a camera.
    /// </summary>
    /// <remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/Transfer_functions_in_imaging#Definition"/>
    /// </remarks>
    /// <param name="s">Scene light</param>
    /// <returns>Digital video signal</returns>
    public static double Oetf(double s)
    {
        if (s == 0)
        {
            return 0;
        }

        double originalSign = s;

        s = Math.Abs(s);

        if (s <= Inverse12)
        {
            return Math.CopySign(Math.Sqrt(3.0 * s), originalSign);
        }

        double e = (A * Math.Log((12 * s) - B)) + C;
        DebugGuard.MustBeGreaterThan(e, 0.0, nameof(e));

        return Math.CopySign(e, originalSign);
    }

    /// <summary>
    /// Inverse Opto-Electronic Transfer Function - converts
    /// digital video signal into a real-world scene light.
    /// </summary>
    /// <remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/Transfer_functions_in_imaging#Definition"/>
    /// </remarks>
    /// <param name="e">Digital video signal</param>
    /// <returns>Scene light</returns>
    public static double InverseOetf(double e)
    {
        if (e == 0)
        {
            return 0;
        }

        double originalSign = e;

        e = Math.Abs(e);

        if (e <= 0.5)
        {
            return Math.CopySign(e * e * (1.0 / 3), originalSign);
        }

        double s = (Math.Exp((e - C) * RA) + B) * Inverse12;
        DebugGuard.MustBeGreaterThan(s, 0.0, nameof(s));

        return Math.CopySign(s, originalSign);
    }

    /// <summary>
    /// Opto-Optical Transfer Function - as-is.
    /// </summary>
    /// <remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/Transfer_functions_in_imaging#Definition"/>
    /// </remarks>
    /// <param name="s">Input signal</param>
    /// <returns>Digital video signal</returns>
    public static double Ootf(double s) => s;

    /// <summary>
    /// Inverse Opto-Optical Transfer Function - as-is.
    /// </summary>
    /// <remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/Transfer_functions_in_imaging#Definition"/>
    /// </remarks>
    /// <param name="s">Digital video signal</param>
    /// <returns>Scene light</returns>
    public static double InverseOotf(double s) => s;
}
