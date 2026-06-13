// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1ReferenceTransform
{
    /******************************************************************************
     * SVT file: test/ref/TxfmRef.cc
     *
     * Reference implementation for txfm, including :
     * - reference_dct_1d
     * - reference_adst_1d
     * - reference_idtx_1d
     * - reference_txfm_1d
     * - reference_txfm_2d
     * - fadst_ref
     *
     * Original authors: Cidana-Edmond, Cidana-Wenyao
     *
     ******************************************************************************/

    public static double GetScaleFactor(Av1Transform2dFlipConfiguration config)
    {
        Span<int> shift = config.Shift;
        int transformWidth = config.TransformSize.GetWidth();
        int transformHeight = config.TransformSize.GetHeight();
        int amplifyBit = shift[0] + shift[1] + shift[2];
        double scaleFactor =
            amplifyBit >= 0 ? (1 << amplifyBit) : (1.0 / (1 << -amplifyBit));

        // For rectangular transforms, we need to multiply by an extra factor.
        int rectType = Av1ForwardTransformer.GetRectangularRatio(transformWidth, transformHeight);
        if (Math.Abs(rectType) == 1)
        {
            scaleFactor *= Math.Sqrt(2);
        }

        return scaleFactor;
    }

    /// <summary>
    /// SVT: reference_txfm_2d
    /// </summary>
    public static void ReferenceTransformFunction2d(Span<double> input, Span<double> output, Av1TransformType transformType, Av1TransformSize transformSize, double scaleFactor)
    {
        // Get transform type and size of each dimension.
        Av1Transform2dFlipConfiguration config = new(transformType, transformSize);
        Av1TransformType1d columnType = GetTransformType1d(config.TransformFunctionTypeColumn);
        Av1TransformType1d rowType = GetTransformType1d(config.TransformFunctionTypeRow);
        int transformWidth = transformSize.GetWidth();
        int transformHeight = transformSize.GetHeight();
        Span<double> tmpInput = new double[transformWidth * transformHeight];
        Span<double> tmpOutput = new double[transformWidth * transformHeight];

        // second forward transform with row_type
        for (int r = 0; r < transformHeight; ++r)
        {
            ReferenceTransform1d(rowType, input[(r * transformWidth)..], output[(r * transformWidth)..], transformWidth);
        }

        // matrix transposition
        for (int r = 0; r < transformHeight; ++r)
        {
            for (int c = 0; c < transformWidth; ++c)
            {
                tmpInput[(c * transformHeight) + r] = output[(r * transformWidth) + c];
            }
        }

        // first forward transform with column_type
        for (int c = 0; c < transformWidth; ++c)
        {
            ReferenceTransform1d(
                columnType,
                tmpInput[(c * transformHeight)..],
                tmpOutput[(c * transformHeight)..],
                transformHeight);
        }

        // matrix transposition
        for (int r = 0; r < transformHeight; ++r)
        {
            for (int c = 0; c < transformWidth; ++c)
            {
                output[(c * transformHeight) + r] = tmpOutput[(r * transformWidth) + c];
            }
        }

        // appropriate scale
        for (int r = 0; r < transformHeight; ++r)
        {
            for (int c = 0; c < transformWidth; ++c)
            {
                output[(r * transformWidth) + c] *= scaleFactor;
            }
        }
    }

    private static void Adst4Reference(Span<int> input, Span<int> output)
    {
        // 16384 * sqrt(2) * sin(kPi/9) * 2 / 3
        const long sinPi19 = 5283;
        const long sinPi29 = 9929;
        const long sinPi39 = 13377;
        const long sinPi49 = 15212;

        long x0, x1, x2, x3;
        long s0, s1, s2, s3, s4, s5, s6, s7;
        x0 = input[0];
        x1 = input[1];
        x2 = input[2];
        x3 = input[3];

        if ((x0 | x1 | x2 | x3) == 0L)
        {
            output[0] = output[1] = output[2] = output[3] = 0;
            return;
        }

        s0 = sinPi19 * x0;
        s1 = sinPi49 * x0;
        s2 = sinPi29 * x1;
        s3 = sinPi19 * x1;
        s4 = sinPi39 * x2;
        s5 = sinPi49 * x3;
        s6 = sinPi29 * x3;
        s7 = x0 + x1 - x3;

        x0 = s0 + s2 + s5;
        x1 = sinPi39 * s7;
        x2 = s1 - s3 + s6;
        x3 = s4;

        s0 = x0 + x3;
        s1 = x1;
        s2 = x2 - x3;
        s3 = x2 - x0 + x3;

        // 1-D transform scaling factor is sqrt(2).
        output[0] = Av1Math.RoundShift(s0, 14);
        output[1] = Av1Math.RoundShift(s1, 14);
        output[2] = Av1Math.RoundShift(s2, 14);
        output[3] = Av1Math.RoundShift(s3, 14);
    }

    private static void ReferenceIdentity1d(Span<double> input, Span<double> output, int size)
    {
        const double sqrt2 = 1.4142135623730950488016887242097f;
        double scale = 0;
        switch (size)
        {
            case 4:
                scale = sqrt2;
                break;
            case 8:
                scale = 2;
                break;
            case 16:
                scale = 2 * sqrt2;
                break;
            case 32:
                scale = 4;
                break;
            case 64:
                scale = 4 * sqrt2;
                break;
            default:
                Assert.Fail();
                break;
        }

        for (int k = 0; k < size; ++k)
        {
            output[k] = input[k] * scale;
        }
    }

    private static void ReferenceDct1d(Span<double> input, Span<double> output, int size)
    {
        const double kInvSqrt2 = 0.707106781186547524400844362104f;
        for (int k = 0; k < size; ++k)
        {
            output[k] = 0;
            for (int n = 0; n < size; ++n)
            {
                output[k] += input[n] * Math.Cos(Math.PI * ((2 * n) + 1) * k / (2 * size));
            }

            if (k == 0)
            {
                output[k] = output[k] * kInvSqrt2;
            }
        }
    }

    private static void ReferenceAdst1d(Span<double> input, Span<double> output, int size)
    {
        if (size == 4)
        {
            // Special case.
            int[] int_input = new int[4];
            for (int i = 0; i < 4; ++i)
            {
                int_input[i] = (int)Math.Round(input[i]);
            }

            int[] int_output = new int[4];
            Adst4Reference(int_input, int_output);
            for (int i = 0; i < 4; ++i)
            {
                output[i] = int_output[i];
            }

            return;
        }

        for (int k = 0; k < size; ++k)
        {
            output[k] = 0;
            for (int n = 0; n < size; ++n)
            {
                output[k] += input[n] * Math.Sin(Math.PI * ((2 * n) + 1) * ((2 * k) + 1) / (4 * size));
            }
        }
    }

    internal static void ReferenceTransform1d(Av1TransformType1d type, Span<double> input, Span<double> output, int size)
    {
        switch (type)
        {
            case Av1TransformType1d.Dct:
                ReferenceDct1d(input, output, size);
                break;
            case Av1TransformType1d.Adst:
            case Av1TransformType1d.FlipAdst:
                ReferenceAdst1d(input, output, size);
                break;
            case Av1TransformType1d.Identity:
                ReferenceIdentity1d(input, output, size);
                break;
            default:
                Assert.Fail();
                break;
        }
    }

    private static Av1TransformType1d GetTransformType1d(Av1TransformFunctionType transformFunctionType)
    {
        switch (transformFunctionType)
        {
            case Av1TransformFunctionType.Dct4:
            case Av1TransformFunctionType.Dct8:
            case Av1TransformFunctionType.Dct16:
            case Av1TransformFunctionType.Dct32:
            case Av1TransformFunctionType.Dct64:
                return Av1TransformType1d.Dct;
            case Av1TransformFunctionType.Adst4:
            case Av1TransformFunctionType.Adst8:
            case Av1TransformFunctionType.Adst16:
            case Av1TransformFunctionType.Adst32:
                return Av1TransformType1d.Adst;
            case Av1TransformFunctionType.Identity4:
            case Av1TransformFunctionType.Identity8:
            case Av1TransformFunctionType.Identity16:
            case Av1TransformFunctionType.Identity32:
            case Av1TransformFunctionType.Identity64:
                return Av1TransformType1d.Identity;
            case Av1TransformFunctionType.Invalid:
            default:
                Assert.Fail();
                return (Av1TransformType1d)5;
        }
    }
}
