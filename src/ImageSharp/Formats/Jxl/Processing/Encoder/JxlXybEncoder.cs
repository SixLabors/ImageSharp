// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// When the color transform is set to XYB, this class lets the encoder
/// convert input RGB color spaces to output XYB.
/// </summary>
internal static class JxlXybEncoder
{
    /// <summary>
    /// Computes the Opsin absorbance for the input R, G, and B colors.
    /// </summary>
    /// <param name="r">The input Red color</param>
    /// <param name="g">The input Green color</param>
    /// <param name="b">The input Blue color</param>
    /// <param name="premulAbsorb">The premultiplied absorbance.</param>
    /// <param name="mixed0">The output X absorbance</param>
    /// <param name="mixed1">The output Y absorbance</param>
    /// <param name="mixed2">The output B absorbance</param>
    private static void OpsinAbsorbance(
        Vector<float> r,
        Vector<float> g,
        Vector<float> b,
        ReadOnlySpan<float> premulAbsorb,
        out Vector<float> mixed0,
        out Vector<float> mixed1,
        out Vector<float> mixed2)
    {
        ReadOnlySpan<float> bias = JxlOpsinConstants.OpsinAbsorbanceBias;

        mixed0 = Vector.FusedMultiplyAdd(
            Vector.Create(premulAbsorb),
            r,
            Vector.FusedMultiplyAdd(
                Vector.Create(premulAbsorb[8..]),
                g,
                Vector.FusedMultiplyAdd(
                    Vector.Create(premulAbsorb[16..]),
                    b,
                    Vector.Create(bias[0]))));

        mixed1 = Vector.FusedMultiplyAdd(
            Vector.Create(premulAbsorb[24..]),
            r,
            Vector.FusedMultiplyAdd(
                Vector.Create(premulAbsorb[32..]),
                g,
                Vector.FusedMultiplyAdd(
                    Vector.Create(premulAbsorb[40..]),
                    b,
                    Vector.Create(bias[1]))));

        mixed2 = Vector.FusedMultiplyAdd(
            Vector.Create(premulAbsorb[48..]),
            r,
            Vector.FusedMultiplyAdd(
                Vector.Create(premulAbsorb[56..]),
                g,
                Vector.FusedMultiplyAdd(
                    Vector.Create(premulAbsorb[64..]),
                    b,
                    Vector.Create(bias[2]))));
    }

    /// <summary>
    /// Stores the input RGB color spaces into XYB spans.
    /// </summary>
    /// <param name="r">Input Red components</param>
    /// <param name="g">Input Green components</param>
    /// <param name="b">Input Blue components</param>
    /// <param name="valX">Output X components</param>
    /// <param name="valY">Output Y components</param>
    /// <param name="valZ">Output B components</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreXYB(
        Vector<float> r,
        Vector<float> g,
        Vector<float> b,
        Span<float> valX,
        Span<float> valY,
        Span<float> valZ)
    {
        Vector<float> half = Vector.Create(0.5f);

        (half * (r - g)).CopyTo(valX);
        (half * (r + g)).CopyTo(valY);
        b.CopyTo(valZ);
    }

    /// <summary>
    /// Converts RGB to XYB. The conversion happens one SIMD vector at a time.
    /// </summary>
    /// <param name="r">Input Red values</param>
    /// <param name="g">Input Green values</param>
    /// <param name="b">Input Blue values</param>
    /// <param name="premulAbsorb">Premultiplied Opsin absorbance.</param>
    /// <param name="valX">Converted R-&gt;X values are stored here.</param>
    /// <param name="valY">Converted G-&gt;Y values are stored here.</param>
    /// <param name="valZ">Converted B-&gt;B values are stored here.</param>
    private static void LinearRgbToXyb(
        Vector<float> r,
        Vector<float> g,
        Vector<float> b,
        ReadOnlySpan<float> premulAbsorb,
        Span<float> valX,
        Span<float> valY,
        Span<float> valZ)
    {
        OpsinAbsorbance(
            r,
            g,
            b,
            premulAbsorb,
            out Vector<float> mixed0,
            out Vector<float> mixed1,
            out Vector<float> mixed2);

        mixed0 = ZeroIfNegative(mixed0);
        mixed1 = ZeroIfNegative(mixed1);
        mixed2 = ZeroIfNegative(mixed2);

        int n = Vector<float>.Count;

        mixed0 = CubeRootAndAdd(
            mixed0,
            Vector.Create(premulAbsorb[(9 * n)..]));

        mixed1 = CubeRootAndAdd(
            mixed1,
            Vector.Create(premulAbsorb[(10 * n)..]));

        mixed2 = CubeRootAndAdd(
            mixed2,
            Vector.Create(premulAbsorb[(11 * n)..]));

        StoreXYB(mixed0, mixed1, mixed2, valX, valY, valZ);
    }

    /// <summary>
    /// Performs an in-place conversion from RGB to XYB. This function
    /// is meant to be invoked on each row. Parameters <paramref name="row0"/>,
    /// <paramref name="row1"/> and <paramref name="row2"/> initially contain
    /// input RGB values. When this function returns, every <paramref name="row0"/>,
    /// <paramref name="row1"/> and <paramref name="row2"/> value will
    /// contain the converted X, Y, and B, respectively.
    /// </summary>
    /// <param name="row0">
    /// This contains input values for the R (Red) component (RGB).
    /// When this method returns, each value in this span will have
    /// the converted X component (XYB).
    /// </param>
    /// <param name="row1">
    /// This contains input values for the G (Green) component (RGB).
    /// When this method returns, each value in this span will have
    /// the converted Y component (XYB).
    /// </param>
    /// <param name="row2">
    /// This contains input values for the B (Blue) component (RGB).
    /// When this method returns, each value in this span will have
    /// the converted B component (XYB).
    /// </param>
    /// <param name="premulAbsorb">
    /// Contains the Opsin absorbance.
    /// </param>
    /// <param name="xSize">
    /// Represents the number of items within each row.
    /// </param>
    public static void LinearRgbRowToXyb(
        Span<float> row0,
        Span<float> row1,
        Span<float> row2,
        ReadOnlySpan<float> premulAbsorb,
        int xSize)
    {
        int n = Vector<float>.Count;

        for (int x = 0; x < xSize; x += n)
        {
            Vector<float> r = Vector.Create<float>(row0[x..]);
            Vector<float> g = Vector.Create<float>(row1[x..]);
            Vector<float> b = Vector.Create<float>(row2[x..]);

            LinearRgbToXyb(
                r,
                g,
                b,
                premulAbsorb,
                row0[x..],
                row1[x..],
                row2[x..]);
        }
    }

    private static Vector<float> CubeRootAndAdd(Vector<float> v, Vector<float> add)
    {
        // HACK: Vector<T> doesn't support cuberoot
        Span<float> span = stackalloc float[Vector<float>.Count];
        v.StoreUnsafe(ref MemoryMarshal.GetReference(span));
        TensorPrimitives.Cbrt(span, span);
        return Vector.LoadUnsafe(ref MemoryMarshal.GetReference(span)) + add;
    }

    private static Vector<float> ZeroIfNegative(Vector<float> v) =>
        Vector.ConditionalSelect(
            Vector.LessThan(v, Vector<float>.Zero),
            Vector<float>.Zero,
            v);

    private static Vector<float> LinearFromSRgb(Vector<float> encoded)
        => JxlSRgbTransferFunction.DisplayFromEncoded(encoded);

    /// <summary>
    /// Converts linear sRGB planes to XYB in place.
    /// </summary>
    /// <param name="premulAbsorb">The broadcast absorbance matrix and cube-root bias coefficients.</param>
    /// <param name="image">The source planes, which receive XYB samples.</param>
    private static void LinearSrgbToXyb(ReadOnlySpan<float> premulAbsorb, JxlImage3<float> image)
    {
        int xSize = image.XSize;

        void ProcessRow(int y, ReadOnlySpan<float> premulAbsorb)
        {
            Span<float> row0 = image.PlaneRow(0, y);
            Span<float> row1 = image.PlaneRow(1, y);
            Span<float> row2 = image.PlaneRow(2, y);

            int lanes = Vector<float>.Count;

            for (int x = 0; x < xSize; x += lanes)
            {
                Vector<float> r = new(row0[x..]);
                Vector<float> g = new(row1[x..]);
                Vector<float> b = new(row2[x..]);

                LinearRgbToXyb(
                    r,
                    g,
                    b,
                    premulAbsorb,
                    row0[x..],
                    row1[x..],
                    row2[x..]);
            }
        }

        for (int y = 0; y < image.YSize; y++)
        {
            ProcessRow(y, premulAbsorb);
        }
    }

    /// <summary>
    /// Converts encoded sRGB planes to XYB in place.
    /// </summary>
    /// <param name="premulAbsorb">The broadcast absorbance matrix and cube-root bias coefficients.</param>
    /// <param name="image">The source planes, which receive XYB samples.</param>
    private static void SRgbToXyb(ReadOnlySpan<float> premulAbsorb, JxlImage3<float> image)
    {
        int xSize = image.XSize;

        void ProcessRow(int y, ReadOnlySpan<float> premulAbsorb)
        {
            Span<float> row0 = image.PlaneRow(0, y);
            Span<float> row1 = image.PlaneRow(1, y);
            Span<float> row2 = image.PlaneRow(2, y);

            int lanes = Vector<float>.Count;

            for (int x = 0; x < xSize; x += lanes)
            {
                Vector<float> r = LinearFromSRgb(new Vector<float>(row0[x..]));
                Vector<float> g = LinearFromSRgb(new Vector<float>(row1[x..]));
                Vector<float> b = LinearFromSRgb(new Vector<float>(row2[x..]));

                LinearRgbToXyb(
                    r,
                    g,
                    b,
                    premulAbsorb,
                    row0[x..],
                    row1[x..],
                    row2[x..]);
            }
        }

        for (int y = 0; y < image.YSize; y++)
        {
            ProcessRow(y, premulAbsorb);
        }
    }

    /// <summary>
    /// Converts encoded sRGB planes to XYB and retains the linear sRGB samples.
    /// </summary>
    /// <param name="premulAbsorb">The broadcast absorbance matrix and cube-root bias coefficients.</param>
    /// <param name="image">The source planes, which receive XYB samples.</param>
    /// <param name="linear">The caller-owned planes that receive linear sRGB samples.</param>
    private static void SrgbToXybAndLinear(ReadOnlySpan<float> premulAbsorb, JxlImage3<float> image, JxlImage3<float> linear)
    {
        int xSize = image.XSize;

        void ProcessRow(int y, ReadOnlySpan<float> premulAbsorb)
        {
            Span<float> rowImage0 = image.PlaneRow(0, y);
            Span<float> rowImage1 = image.PlaneRow(1, y);
            Span<float> rowImage2 = image.PlaneRow(2, y);

            Span<float> rowLinear0 = linear.PlaneRow(0, y);
            Span<float> rowLinear1 = linear.PlaneRow(1, y);
            Span<float> rowLinear2 = linear.PlaneRow(2, y);

            int lanes = Vector<float>.Count;

            for (int x = 0; x < xSize; x += lanes)
            {
                Vector<float> r = LinearFromSRgb(new Vector<float>(rowImage0[x..]));
                Vector<float> g = LinearFromSRgb(new Vector<float>(rowImage1[x..]));
                Vector<float> b = LinearFromSRgb(new Vector<float>(rowImage2[x..]));

                r.CopyTo(rowLinear0[x..]);
                g.CopyTo(rowLinear1[x..]);
                b.CopyTo(rowLinear2[x..]);

                LinearRgbToXyb(
                    r,
                    g,
                    b,
                    premulAbsorb,
                    rowImage0[x..],
                    rowImage1[x..],
                    rowImage2[x..]);
            }
        }

        for (int y = 0; y < image.YSize; y++)
        {
            ProcessRow(y, premulAbsorb);
        }
    }

    private static void ComputePremulAbsorb(float intensityTarget, Span<float> premulAbsorb)
    {
        float mul = intensityTarget / 255.0f;
        int lanes = Vector<float>.Count;

        for (int j = 0; j < 3; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                float absorb = JxlOpsinAbsorbanceMatrix[j][i] * mul;

                premulAbsorb
                    .Slice(((j * 3) + i) * lanes, lanes)
                    .Fill(absorb);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            float negBiasCbrt = -MathF.Cbrt(JxlOpsinAbsorbanceBias[i]);

            premulAbsorb
                .Slice((9 + i) * lanes, lanes)
                .Fill(negBiasCbrt);
        }
    }

    public static bool ToXyb(
        Configuration configuration,
        JxlColorEncoding currentEncoding,
        float intensityTarget,
        JxlPlane<float>? black,
        JxlImage3<float> image,
        JxlCmsInterface cms,
        JxlImage3<float>? linear)
    {
        if (black is not null)
        {
            if (image.GetRectangle() != black.GetRectangle())
            {
                return false;
            }
        }

        if (linear is not null)
        {
            if (image.GetRectangle() != linear.GetRectangle())
            {
                return false;
            }
        }

        int lanes = Vector<float>.Count;

        // Twelve coefficients are broadcast across the active vector lanes. The bounded
        // workspace stays on the calling thread and is reused for every row of this transform.
        Span<float> premulAbsorb = stackalloc float[lanes * 12];
        ComputePremulAbsorb(intensityTarget, premulAbsorb);

        JxlColorEncoding linearSrgb = JxlColorEncoding.LinearSRgb(currentEncoding.IsGray);

        if (linearSrgb.SameColorEncoding(currentEncoding))
        {
            if (linear is not null)
            {
                JxlImageOperations.ForceCopyImage(image, linear);
            }

            LinearSrgbToXyb(premulAbsorb, image);
            return true;
        }

        if (currentEncoding.IsSRgb)
        {
            if (linear is not null)
            {
                SrgbToXybAndLinear(premulAbsorb, image, linear);
                return true;
            }

            SRgbToXyb(premulAbsorb, image);
            return true;
        }

        // TODO
        ApplyColorTransform(
            configuration,
            currentEncoding,
            intensityTarget,
            image,
            black,
            image.GetRectangle(),
            linearSrgb,
            cms,
            linear ?? image);

        if (linear is not null)
        {
            JxlImageOperations.ForceCopyImage(linear, image);
        }

        LinearSrgbToXyb(premulAbsorb, image);
        return true;
    }

    public static void RgbToYcbcr(
        Configuration configuration,
        JxlPlane<float> rPlane,
        JxlPlane<float> gPlane,
        JxlPlane<float> bPlane,
        JxlPlane<float> yPlane,
        JxlPlane<float> cbPlane,
        JxlPlane<float> crPlane)
    {
        int xSize = rPlane.XSize;
        int ySize = rPlane.YSize;

        if (xSize == 0 || ySize == 0)
        {
            return;
        }

        Vector<float> k128 = new(128.0f / 255);
        Vector<float> kR = new(0.299f);
        Vector<float> kG = new(0.587f);
        Vector<float> kB = new(0.114f);
        Vector<float> kAmpR = new(0.701f);
        Vector<float> kAmpB = new(0.886f);

        Vector<float> kDiffR = kAmpR + kR;
        Vector<float> kDiffB = kAmpB + kB;

        Vector<float> kNormR = Vector<float>.One / (kAmpR + kG + kB);
        Vector<float> kNormB = Vector<float>.One / (kR + kG + kAmpB);

        int step = Vector<float>.Count;

        int groupArea = JxlFrameDimensions.GroupDimensions * JxlFrameDimensions.GroupDimensions;
        int linesPerGroup = JxlMath.DivCeil(groupArea, xSize);
        int numStripes = JxlMath.DivCeil(ySize, linesPerGroup);

        void Transform(int index)
        {
            int y0 = index * linesPerGroup;
            int y1 = Math.Min(y0 + linesPerGroup, ySize);

            for (int y = y0; y < y1; y++)
            {
                ReadOnlySpan<float> rRow = rPlane.GetRow(y);
                ReadOnlySpan<float> gRow = gPlane.GetRow(y);
                ReadOnlySpan<float> bRow = bPlane.GetRow(y);

                Span<float> yRow = yPlane.GetRow(y);
                Span<float> cbRow = cbPlane.GetRow(y);
                Span<float> crRow = crPlane.GetRow(y);

                for (int x = 0; x < xSize; x += step)
                {
                    Vector<float> r = new(rRow[x..]);
                    Vector<float> g = new(gRow[x..]);
                    Vector<float> b = new(bRow[x..]);

                    Vector<float> rBase = r * kR;
                    Vector<float> rDiff = r * kDiffR;
                    Vector<float> gBase = g * kG;
                    Vector<float> bBase = b * kB;
                    Vector<float> bDiff = b * kDiffB;

                    Vector<float> yBase = rBase + gBase + bBase;
                    Vector<float> yVec = yBase - k128;
                    Vector<float> cbVec = (bDiff - yBase) * kNormB;
                    Vector<float> crVec = (rDiff - yBase) * kNormR;

                    yVec.CopyTo(yRow[x..]);
                    cbVec.CopyTo(cbRow[x..]);
                    crVec.CopyTo(crRow[x..]);
                }
            }
        }

        for (int i = 0; i < numStripes; i++)
        {
            Transform(i);
        }
    }
}
