// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>Reconstructs linear RGB from JPEG XL XYB samples.</summary>
internal static class JxlXybDecoder
{
    /// <summary>Converts three equally sized XYB rows to linear RGB without clamping reconstructed components.</summary>
    /// <param name="opsinX">The logical X samples.</param>
    /// <param name="opsinY">The corresponding Y samples.</param>
    /// <param name="opsinB">The corresponding B samples.</param>
    /// <param name="linearR">The equally sized red destination.</param>
    /// <param name="linearG">The equally sized green destination.</param>
    /// <param name="linearB">The equally sized blue destination.</param>
    /// <param name="parameters">The matrix and biases prepared before processing rows.</param>
    /// <remarks>Corresponding source and destination rows can start at the same address. Offset overlap is not supported.</remarks>
    public static void ConvertXybToRgb(
        ReadOnlySpan<float> opsinX,
        ReadOnlySpan<float> opsinY,
        ReadOnlySpan<float> opsinB,
        Span<float> linearR,
        Span<float> linearG,
        Span<float> linearB,
        JxlOpsinParameters parameters)
    {
        Matrix4x4 matrix = parameters.InverseOpsinMatrix;
        Vector3 biases = parameters.OpsinBiases;
        Vector3 cubeRootBiases = parameters.OpsinBiasesCbrt;
        ref float inputX = ref MemoryMarshal.GetReference(opsinX);
        ref float inputY = ref MemoryMarshal.GetReference(opsinY);
        ref float inputB = ref MemoryMarshal.GetReference(opsinB);
        ref float outputR = ref MemoryMarshal.GetReference(linearR);
        ref float outputG = ref MemoryMarshal.GetReference(linearG);
        ref float outputB = ref MemoryMarshal.GetReference(linearB);
        int x = 0;

        // Each register contains one component from consecutive pixels. Load all three components before the first store.
        // Only complete registers enter a SIMD loop. Narrower widths and the scalar tail start at the first unprocessed pixel.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= opsinX.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> input0 = Vector512.LoadUnsafe(ref inputX, (uint)x);
                Vector512<float> input1 = Vector512.LoadUnsafe(ref inputY, (uint)x);
                Vector512<float> input2 = Vector512.LoadUnsafe(ref inputB, (uint)x);
                ConvertXybToRgb(
                    input0,
                    input1,
                    input2,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector512<float> r,
                    out Vector512<float> g,
                    out Vector512<float> b);

                r.StoreUnsafe(ref outputR, (uint)x);
                g.StoreUnsafe(ref outputG, (uint)x);
                b.StoreUnsafe(ref outputB, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= opsinX.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> input0 = Vector256.LoadUnsafe(ref inputX, (uint)x);
                Vector256<float> input1 = Vector256.LoadUnsafe(ref inputY, (uint)x);
                Vector256<float> input2 = Vector256.LoadUnsafe(ref inputB, (uint)x);
                ConvertXybToRgb(
                    input0,
                    input1,
                    input2,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector256<float> r,
                    out Vector256<float> g,
                    out Vector256<float> b);

                r.StoreUnsafe(ref outputR, (uint)x);
                g.StoreUnsafe(ref outputG, (uint)x);
                b.StoreUnsafe(ref outputB, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= opsinX.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> input0 = Vector128.LoadUnsafe(ref inputX, (uint)x);
                Vector128<float> input1 = Vector128.LoadUnsafe(ref inputY, (uint)x);
                Vector128<float> input2 = Vector128.LoadUnsafe(ref inputB, (uint)x);
                ConvertXybToRgb(
                    input0,
                    input1,
                    input2,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector128<float> r,
                    out Vector128<float> g,
                    out Vector128<float> b);

                r.StoreUnsafe(ref outputR, (uint)x);
                g.StoreUnsafe(ref outputG, (uint)x);
                b.StoreUnsafe(ref outputB, (uint)x);
            }
        }

        for (; x < opsinX.Length; x++)
        {
            ConvertXybToRgb(opsinX[x], opsinY[x], opsinB[x], in matrix, biases, cubeRootBiases, out float r, out float g, out float b);
            linearR[x] = r;
            linearG[x] = g;
            linearB[x] = b;
        }
    }

    /// <summary>Converts an XYB rectangle to an equally sized linear RGB image.</summary>
    /// <param name="opsin">The source planes that contain the rectangle.</param>
    /// <param name="rect">The logical source rectangle.</param>
    /// <param name="linear">The destination, whose logical dimensions must equal the rectangle dimensions.</param>
    /// <param name="parameters">The prepared matrix and biases.</param>
    /// <returns>Whether the rectangle and destination have equal dimensions.</returns>
    public static bool OpsinToLinear(JxlImage3<float> opsin, Rectangle rect, JxlImage3<float> linear, JxlOpsinParameters parameters)
    {
        if (!JxlImageOperations.SameSize(rect, linear))
        {
            return false;
        }

        // Borrow all allocations once. Rectangle origins belong only to the input; the output starts at its own origin.
        // Source and destination strides can differ. Exact row slices keep every vector and scalar store out of padding.
        ReadOnlySpan<float> source0 = opsin.PlaneRow(0, 0);
        ReadOnlySpan<float> source1 = opsin.PlaneRow(1, 0);
        ReadOnlySpan<float> source2 = opsin.PlaneRow(2, 0);
        Span<float> destination0 = linear.PlaneRow(0, 0);
        Span<float> destination1 = linear.PlaneRow(1, 0);
        Span<float> destination2 = linear.PlaneRow(2, 0);
        int sourceStride = opsin.PixelsPerRow;
        int destinationStride = linear.PixelsPerRow;

        for (int y = 0; y < rect.Height; y++)
        {
            int sourceOffset = ((rect.Top + y) * sourceStride) + rect.Left;
            int destinationOffset = y * destinationStride;
            ConvertXybToRgb(
                source0.Slice(sourceOffset, rect.Width),
                source1.Slice(sourceOffset, rect.Width),
                source2.Slice(sourceOffset, rect.Width),
                destination0.Slice(destinationOffset, rect.Width),
                destination1.Slice(destinationOffset, rect.Width),
                destination2.Slice(destinationOffset, rect.Width),
                parameters);
        }

        return true;
    }

    /// <summary>Applies the inverse opsin equations to one pixel in the same order as each SIMD lane.</summary>
    /// <param name="x">The X sample.</param>
    /// <param name="y">The Y sample.</param>
    /// <param name="b">The B sample.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The negative absorbance biases.</param>
    /// <param name="cubeRootBiases">The signed cube roots of the negative biases.</param>
    /// <param name="r">The reconstructed red sample.</param>
    /// <param name="g">The reconstructed green sample.</param>
    /// <param name="blue">The reconstructed blue sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertXybToRgb(
        float x,
        float y,
        float b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out float r,
        out float g,
        out float blue)
    {
        // Undo the opponent mix and the forward transform's cube-root offset before cubing.
        float gammaR = (y + x) - cubeRootBiases.X;
        float gammaG = (y - x) - cubeRootBiases.Y;
        float gammaB = b - cubeRootBiases.Z;
        float mixedR = float.MultiplyAddEstimate(gammaR * gammaR, gammaR, biases.X);
        float mixedG = float.MultiplyAddEstimate(gammaG * gammaG, gammaG, biases.Y);
        float mixedB = float.MultiplyAddEstimate(gammaB * gammaB, gammaB, biases.Z);

        // The runtime uses the same X, then Y, then Z multiply-add order as shared Transform3.
        Vector3 linear = Vector3.Transform(new Vector3(mixedR, mixedG, mixedB), matrix);
        r = linear.X;
        g = linear.Y;
        blue = linear.Z;
    }

    /// <summary>Reconstructs 4 independent pixels; each register holds one component in pixel order.</summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The negative absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertXybToRgb(
        Vector128<float> x,
        Vector128<float> y,
        Vector128<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector128<float> r,
        out Vector128<float> g,
        out Vector128<float> blue)
    {
        Vector128<float> gammaR = (y + x) - Vector128.Create(cubeRootBiases.X);
        Vector128<float> gammaG = (y - x) - Vector128.Create(cubeRootBiases.Y);
        Vector128<float> gammaB = b - Vector128.Create(cubeRootBiases.Z);
        Vector128<float> mixedR = Vector128.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector128.Create(biases.X));
        Vector128<float> mixedG = Vector128.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector128.Create(biases.Y));
        Vector128<float> mixedB = Vector128.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector128.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>Reconstructs 8 independent pixels; each register holds one component in pixel order.</summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The negative absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertXybToRgb(
        Vector256<float> x,
        Vector256<float> y,
        Vector256<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector256<float> r,
        out Vector256<float> g,
        out Vector256<float> blue)
    {
        Vector256<float> gammaR = (y + x) - Vector256.Create(cubeRootBiases.X);
        Vector256<float> gammaG = (y - x) - Vector256.Create(cubeRootBiases.Y);
        Vector256<float> gammaB = b - Vector256.Create(cubeRootBiases.Z);
        Vector256<float> mixedR = Vector256.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector256.Create(biases.X));
        Vector256<float> mixedG = Vector256.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector256.Create(biases.Y));
        Vector256<float> mixedB = Vector256.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector256.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>Reconstructs 16 independent pixels; each register holds one component in pixel order.</summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The negative absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertXybToRgb(
        Vector512<float> x,
        Vector512<float> y,
        Vector512<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector512<float> r,
        out Vector512<float> g,
        out Vector512<float> blue)
    {
        Vector512<float> gammaR = (y + x) - Vector512.Create(cubeRootBiases.X);
        Vector512<float> gammaG = (y - x) - Vector512.Create(cubeRootBiases.Y);
        Vector512<float> gammaB = b - Vector512.Create(cubeRootBiases.Z);
        Vector512<float> mixedR = Vector512.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector512.Create(biases.X));
        Vector512<float> mixedG = Vector512.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector512.Create(biases.Y));
        Vector512<float> mixedB = Vector512.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector512.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>Checks whether native reconstruction supports the encoding's direct fields and transfer function.</summary>
    /// <param name="colorEncoding">The requested encoding.</param>
    /// <returns>Whether its direct color fields can describe supported reconstruction output.</returns>
    public static bool CanOutputToColorEncoding(JxlColorEncoding colorEncoding)
    {
        if (!colorEncoding.HaveFields)
        {
            return false;
        }

        JxlCustomTransferFunction tf = colorEncoding.TransferFunction;

        if (!tf.IsPq && !tf.IsSrgb && !tf.HaveGamma && !tf.IsLinear && !tf.IsHlg && !tf.IsDci && !tf.Is709)
        {
            return false;
        }

        if (colorEncoding.IsGray && colorEncoding.WhitePoint != JxlWhitePoint.D65)
        {
            return false;
        }

        return true;
    }
}
