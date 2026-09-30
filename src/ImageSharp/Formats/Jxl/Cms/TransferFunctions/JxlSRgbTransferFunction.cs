// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

internal static class JxlSRgbTransferFunction
{
    private const float ThresholdSRGBToLinear = 0.04045f;
    private const float ThresholdLinearToSRGB = 0.0031308f;
    private const float LowDiv = 12.92f;
    private const float LowDivInverse = 1.0f / LowDiv;

    private static ReadOnlySpan<float> DisplayP =>
    [
        2.200248328e-04f, 2.200248328e-04f, 2.200248328e-04f, 2.200248328e-04f,
        1.043637593e-02f, 1.043637593e-02f, 1.043637593e-02f, 1.043637593e-02f,
        1.624820318e-01f, 1.624820318e-01f, 1.624820318e-01f, 1.624820318e-01f,
        7.961564959e-01f, 7.961564959e-01f, 7.961564959e-01f, 7.961564959e-01f,
        8.210152774e-01f, 8.210152774e-01f, 8.210152774e-01f, 8.210152774e-01f
    ];

    private static ReadOnlySpan<float> DisplayQ =>
    [
        2.631846970e-01f, 2.631846970e-01f, 2.631846970e-01f, 2.631846970e-01f,
        1.076976492e+00f, 1.076976492e+00f, 1.076976492e+00f, 1.076976492e+00f,
        4.987528350e-01f, 4.987528350e-01f, 4.987528350e-01f, 4.987528350e-01f,
        -5.512498495e-02f, -5.512498495e-02f, -5.512498495e-02f, -5.512498495e-02f,
        6.521209011e-03f, 6.521209011e-03f, 6.521209011e-03f, 6.521209011e-03f
    ];

    private static ReadOnlySpan<float> EncodedP =>
    [
        -5.135152395e-04f, -5.135152395e-04f, -5.135152395e-04f, -5.135152395e-04f,
        5.287254571e-03f, 5.287254571e-03f, 5.287254571e-03f, 5.287254571e-03f,
        3.903842876e-01f, 3.903842876e-01f, 3.903842876e-01f, 3.903842876e-01f,
        1.474205315e+00f, 1.474205315e+00f, 1.474205315e+00f, 1.474205315e+00f,
        7.352629620e-01f, 7.352629620e-01f, 7.352629620e-01f, 7.352629620e-01f
    ];

    private static ReadOnlySpan<float> EncodedQ =>
    [
        1.004519624e-02f, 1.004519624e-02f, 1.004519624e-02f, 1.004519624e-02f,
        3.036675394e-01f, 3.036675394e-01f, 3.036675394e-01f, 3.036675394e-01f,
        1.340816930e+00f, 1.340816930e+00f, 1.340816930e+00f, 1.340816930e+00f,
        9.258482155e-01f, 9.258482155e-01f, 9.258482155e-01f, 9.258482155e-01f,
        2.424867759e-02f, 2.424867759e-02f, 2.424867759e-02f, 2.424867759e-02f
    ];

    public static Vector<float> DisplayFromEncoded(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);

        Vector<float> linear = x * Vector.Create(LowDivInverse);
        Vector<float> poly = EvaluateRationalPolynomial(x, DisplayP, DisplayQ);
        Vector<float> magnitude = Vector.ConditionalSelect(
            Vector.GreaterThan(x, Vector.Create(ThresholdSRGBToLinear)),
            poly,
            linear);

        return Vector.AndNot(sign, magnitude) | originalSign;
    }

    public static Vector<float> EncodedFromDisplay(Vector<float> x)
    {
        Vector<float> sign = Vector.Create(0x80000000u).As<uint, float>();
        Vector<float> originalSign = x & sign;
        x = Vector.AndNot(sign, x);

        Vector<float> linear = x * Vector.Create(LowDiv);
        Vector<float> poly = EvaluateRationalPolynomial(Vector.SquareRoot(x), DisplayP, DisplayQ);
        Vector<float> magnitude = Vector.ConditionalSelect(
            Vector.GreaterThan(x, Vector.Create(ThresholdLinearToSRGB)),
            poly,
            linear);

        return Vector.AndNot(sign, magnitude) | originalSign;
    }
}
