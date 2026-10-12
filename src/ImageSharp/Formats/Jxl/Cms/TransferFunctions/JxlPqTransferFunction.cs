// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

/// <summary>
/// Perceptual Quantization transfer function.
/// </summary>
internal struct JxlPqTransferFunction(float displayIntensityTarget = DefaultIntensityTarget)
{
    private const double M1 = 2610.0 / 16384;
    private const double M2 = (2523.0 / 4096) * 128;
    private const double C1 = 3424.0 / 4096;
    private const double C2 = (2413.0 / 4096) * 32;
    private const double C3 = (2392.0 / 4096) * 32;

    private readonly float scaleFactorTo10000Nits = displayIntensityTarget * (1.0f / 10000.0f);
    private readonly float scaleFactorFrom10000Nits = 10000.0f / displayIntensityTarget;

    private static ReadOnlySpan<float> DisplayP =>
    [
        2.62975656e-04f, 2.62975656e-04f, 2.62975656e-04f, 2.62975656e-04f,
        -6.23553089e-03f, -6.23553089e-03f, -6.23553089e-03f, -6.23553089e-03f,
        7.38602301e-01f, 7.38602301e-01f, 7.38602301e-01f, 7.38602301e-01f,
        2.64553172e+00f, 2.64553172e+00f, 2.64553172e+00f, 2.64553172e+00f,
        5.50034862e-01f, 5.50034862e-01f, 5.50034862e-01f, 5.50034862e-01f
    ];

    private static ReadOnlySpan<float> DisplayQ =>
    [
        4.21350107e+02f, 4.21350107e+02f, 4.21350107e+02f, 4.21350107e+02f,
        -4.28736818e+02f, -4.28736818e+02f, -4.28736818e+02f, -4.28736818e+02f,
        1.74364667e+02f, 1.74364667e+02f, 1.74364667e+02f, 1.74364667e+02f,
        -3.39078883e+01f, -3.39078883e+01f, -3.39078883e+01f, -3.39078883e+01f,
        2.67718770e+00f, 2.67718770e+00f, 2.67718770e+00f, 2.67718770e+00f
    ];

    private static ReadOnlySpan<float> EncodedP =>
    [
        1.351392e-02f, 1.351392e-02f, 1.351392e-02f, 1.351392e-02f,
        -1.095778e+00f, -1.095778e+00f, -1.095778e+00f, -1.095778e+00f,
        5.522776e+01f, 5.522776e+01f, 5.522776e+01f, 5.522776e+01f,
        1.492516e+02f, 1.492516e+02f, 1.492516e+02f, 1.492516e+02f,
        4.838434e+01f, 4.838434e+01f, 4.838434e+01f, 4.838434e+01f
    ];

    private static ReadOnlySpan<float> EncodedQ =>
    [
        1.012416e+00f, 1.012416e+00f, 1.012416e+00f, 1.012416e+00f,
        2.016708e+01f, 2.016708e+01f, 2.016708e+01f, 2.016708e+01f,
        9.263710e+01f, 9.263710e+01f, 9.263710e+01f, 9.263710e+01f,
        1.120607e+02f, 1.120607e+02f, 1.120607e+02f, 1.120607e+02f,
        2.590418e+01f, 2.590418e+01f, 2.590418e+01f, 2.590418e+01f
    ];

    private static ReadOnlySpan<float> EncodedPLo =>
    [
        9.863406e-06f, 9.863406e-06f, 9.863406e-06f, 9.863406e-06f,
        3.881234e-01f, 3.881234e-01f, 3.881234e-01f, 3.881234e-01f,
        1.352821e+02f, 1.352821e+02f, 1.352821e+02f, 1.352821e+02f,
        6.889862e+04f, 6.889862e+04f, 6.889862e+04f, 6.889862e+04f,
        -2.864824e+05f, -2.864824e+05f, -2.864824e+05f, -2.864824e+05f
    ];

    private static ReadOnlySpan<float> EncodedQLo =>
    [
        3.371868e+01f, 3.371868e+01f, 3.371868e+01f, 3.371868e+01f,
        1.477719e+03f, 1.477719e+03f, 1.477719e+03f, 1.477719e+03f,
        1.608477e+04f, 1.608477e+04f, 1.608477e+04f, 1.608477e+04f,
        -4.389884e+04f, -4.389884e+04f, -4.389884e+04f, -4.389884e+04f,
        -2.072546e+05f, -2.072546e+05f, -2.072546e+05f, -2.072546e+05f
    ];

    public static double DisplayFromEncoded(float displayIntensityTarget, double e)
    {
        if (e == 0.0)
        {
            return 0.0;
        }

        double originalSign = e;

        e = Math.Abs(e);

        double xp = Math.Pow(e, 1.0 / M2);
        double num = Math.Max(xp - C1, 0.0);
        double den = C2 - (C3 * xp);
        double d = Math.Pow(num / den, 1.0 / M1);

        return Math.CopySign(d * (10000.0 / displayIntensityTarget), originalSign);
    }

    public static double EncodedFromDisplay(float displayIntensityTarget, double d)
    {
        if (d == 0.0)
        {
            return 0.0;
        }

        double originalSign = d;

        d = Math.Abs(d);

        double xp = Math.Pow(d * (displayIntensityTarget * (1 / 10000)), M1);
        double num = C1 + (xp * C2);
        double den = 1.0 + (xp * C3);
        double e = Math.Pow(num / den, M2);

        return Math.CopySign(e, originalSign);
    }

    public Vector<float> DisplayFromEncoded(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);

        Vector<float> xpxx = (x * x) + x;
        Vector<float> magnitude = EvaluateRationalPolynomial(xpxx, DisplayP, DisplayQ);

        return Vector.AndNot(sign, magnitude * Vector.Create(this.scaleFactorFrom10000Nits)) | originalSign;
    }

    public Vector<float> EncodedFromDisplay(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);

        Vector<float> xto025 = Vector.SquareRoot(Vector.SquareRoot(x * Vector.Create(this.scaleFactorTo10000Nits)));

        Vector<float> magnitude = Vector.ConditionalSelect(
            Vector.LessThan(x, Vector.Create(1e-4f)),
            EvaluateRationalPolynomial(xto025, EncodedPLo, EncodedQLo),
            EvaluateRationalPolynomial(xto025, EncodedP, EncodedQ));

        return Vector.AndNot(sign, magnitude) | originalSign;
    }
}
