// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Converts linear or encoded sRGB planes to the encoder's XYB samples.
/// </summary>
internal static class JxlXybEncoder
{
    /// <summary>
    /// Converts source planes to XYB and optionally retains the expanded linear samples.
    /// </summary>
    /// <typeparam name="TOperator">The shared identity or sRGB expansion operator selected before traversal.</typeparam>
    /// <param name="matrix">The intensity-scaled forward absorbance matrix.</param>
    /// <param name="biases">The signed inverse biases, subtracted before forward root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after forward root evaluation.</param>
    /// <param name="source">The source planes, which can also be the XYB destination.</param>
    /// <param name="destination">The equally sized XYB destination.</param>
    /// <param name="linear">The optional equally sized destination for expanded linear RGB.</param>
    /// <param name="operation">The input component operation.</param>
    private static void RgbToXyb<TOperator>(
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        JxlImage3<float> source,
        JxlImage3<float> destination,
        JxlImage3<float>? linear,
        TOperator operation)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        // Borrow every allocation before row processing. Independent source, XYB and retained-linear owners can have different strides.
        ReadOnlySpan<float> input0 = source.PlaneRow(0, 0);
        ReadOnlySpan<float> input1 = source.PlaneRow(1, 0);
        ReadOnlySpan<float> input2 = source.PlaneRow(2, 0);
        Span<float> output0 = destination.PlaneRow(0, 0);
        Span<float> output1 = destination.PlaneRow(1, 0);
        Span<float> output2 = destination.PlaneRow(2, 0);
        Span<float> linear0 = linear is null ? [] : linear.PlaneRow(0, 0);
        Span<float> linear1 = linear is null ? [] : linear.PlaneRow(1, 0);
        Span<float> linear2 = linear is null ? [] : linear.PlaneRow(2, 0);
        int sourceStride = source.PixelsPerRow;
        int destinationStride = destination.PixelsPerRow;
        int linearStride = linear is null ? 0 : linear.PixelsPerRow;
        int width = source.XSize;

        for (int y = 0; y < source.YSize; y++)
        {
            int inputOffset = y * sourceStride;
            int outputOffset = y * destinationStride;
            int linearOffset = y * linearStride;
            RgbRowToXyb(
                input0.Slice(inputOffset, width),
                input1.Slice(inputOffset, width),
                input2.Slice(inputOffset, width),
                output0.Slice(outputOffset, width),
                output1.Slice(outputOffset, width),
                output2.Slice(outputOffset, width),
                linear is null ? [] : linear0.Slice(linearOffset, width),
                linear is null ? [] : linear1.Slice(linearOffset, width),
                linear is null ? [] : linear2.Slice(linearOffset, width),
                in matrix,
                biases,
                cubeRootBiases,
                operation);
        }
    }

    /// <summary>
    /// Fuses shared input expansion, optional linear output and XYB reconstruction over one logical row.
    /// </summary>
    /// <typeparam name="TOperator">The shared input operation.</typeparam>
    /// <param name="inputR">The finite normalized red samples within the pixel range.</param>
    /// <param name="inputG">The corresponding green samples.</param>
    /// <param name="inputB">The corresponding blue samples.</param>
    /// <param name="outputX">The equally sized X destination.</param>
    /// <param name="outputY">The equally sized Y destination.</param>
    /// <param name="outputB">The equally sized B destination.</param>
    /// <param name="linearR">The optional red destination, empty when no linear output is requested.</param>
    /// <param name="linearG">The matching optional green destination.</param>
    /// <param name="linearB">The matching optional blue destination.</param>
    /// <param name="matrix">The intensity-scaled forward absorbance matrix.</param>
    /// <param name="biases">The signed inverse biases, subtracted before forward root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after forward root evaluation.</param>
    /// <param name="operation">The shared input component operation.</param>
    /// <remarks>
    /// All logical rows have equal lengths. RGB and XYB rows can start at the same address; offset overlap is not supported.
    /// Input components use the normalized finite pixel range [-65504,65504]. Encoded input must use the shared bounded expansion operator.
    /// Supply three empty linear rows when retention is disabled. Retained linear storage must be separate from XYB output storage.
    /// </remarks>
    public static void RgbRowToXyb<TOperator>(
        ReadOnlySpan<float> inputR,
        ReadOnlySpan<float> inputG,
        ReadOnlySpan<float> inputB,
        Span<float> outputX,
        Span<float> outputY,
        Span<float> outputB,
        Span<float> linearR,
        Span<float> linearG,
        Span<float> linearB,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        TOperator operation)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        ref float sourceR = ref MemoryMarshal.GetReference(inputR);
        ref float sourceG = ref MemoryMarshal.GetReference(inputG);
        ref float sourceB = ref MemoryMarshal.GetReference(inputB);
        ref float destinationX = ref MemoryMarshal.GetReference(outputX);
        ref float destinationY = ref MemoryMarshal.GetReference(outputY);
        ref float destinationB = ref MemoryMarshal.GetReference(outputB);
        ref float retainedR = ref MemoryMarshal.GetReference(linearR);
        ref float retainedG = ref MemoryMarshal.GetReference(linearG);
        ref float retainedB = ref MemoryMarshal.GetReference(linearB);
        bool retainLinear = !linearR.IsEmpty;
        int x = 0;

        // Each register holds one component from consecutive pixels. The selected operation owns input expansion and its result safety.
        // Load RGB before storing either output. Every complete SIMD block and scalar remainder uses the same prepared transform.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> r = operation.Invoke(Vector512.LoadUnsafe(ref sourceR, (uint)x));
                Vector512<float> g = operation.Invoke(Vector512.LoadUnsafe(ref sourceG, (uint)x));
                Vector512<float> b = operation.Invoke(Vector512.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                Xyb.ConvertFromRgb(
                    r,
                    g,
                    b,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector512<float> valueX,
                    out Vector512<float> valueY,
                    out Vector512<float> valueB);

                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> r = operation.Invoke(Vector256.LoadUnsafe(ref sourceR, (uint)x));
                Vector256<float> g = operation.Invoke(Vector256.LoadUnsafe(ref sourceG, (uint)x));
                Vector256<float> b = operation.Invoke(Vector256.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                Xyb.ConvertFromRgb(
                    r,
                    g,
                    b,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector256<float> valueX,
                    out Vector256<float> valueY,
                    out Vector256<float> valueB);

                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> r = operation.Invoke(Vector128.LoadUnsafe(ref sourceR, (uint)x));
                Vector128<float> g = operation.Invoke(Vector128.LoadUnsafe(ref sourceG, (uint)x));
                Vector128<float> b = operation.Invoke(Vector128.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                Xyb.ConvertFromRgb(
                    r,
                    g,
                    b,
                    in matrix,
                    biases,
                    cubeRootBiases,
                    out Vector128<float> valueX,
                    out Vector128<float> valueY,
                    out Vector128<float> valueB);

                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        for (; x < inputR.Length; x++)
        {
            float r = operation.Invoke(inputR[x]);
            float g = operation.Invoke(inputG[x]);
            float b = operation.Invoke(inputB[x]);
            if (retainLinear)
            {
                linearR[x] = r;
                linearG[x] = g;
                linearB[x] = b;
            }

            Xyb.ConvertFromRgb(r, g, b, in matrix, biases, cubeRootBiases, out float valueX, out float valueY, out float valueB);
            outputX[x] = valueX;
            outputY[x] = valueY;
            outputB[x] = valueB;
        }
    }

    /// <summary>
    /// Converts the image's direct sRGB samples to XYB, optionally retaining linear sRGB.
    /// </summary>
    /// <param name="configuration">The configuration used by the general color conversion path.</param>
    /// <param name="currentEncoding">The source color encoding.</param>
    /// <param name="intensityTarget">The positive original intensity target, in nits.</param>
    /// <param name="black">The optional source black channel used by the general color conversion path.</param>
    /// <param name="image">The equally sized source planes, replaced with XYB samples.</param>
    /// <param name="cms">The general color conversion provider.</param>
    /// <param name="linear">The optional equally sized output that retains expanded linear sRGB.</param>
    /// <returns>Whether the source and requested output planes have matching dimensions.</returns>
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

        // The shared default retains the exact native forward matrix. Scale it once for this image's original intensity.
        // Source expansion, optional linear storage and shared XYB arithmetic then use one pass.
        XybTransform transform = KnownXybTransforms.Default;
        Matrix4x4 matrix = transform.Forward * (intensityTarget / XybTransform.ReferenceIntensity);
        Vector3 biases = transform.Biases;
        Vector3 cubeRootBiases = transform.CubeRootBiases;
        bool directSrgb = currentEncoding.HaveFields
            && currentEncoding.WhitePoint == JxlWhitePoint.D65
            && (currentEncoding.ColorSpace == JxlColorSpace.Gray
                || (currentEncoding.ColorSpace == JxlColorSpace.Rgb && currentEncoding.Primaries == JxlPrimaries.SRgb));

        if (directSrgb && currentEncoding.TransferFunction.IsLinear)
        {
            RgbToXyb(in matrix, biases, cubeRootBiases, image, image, linear, default(TensorPrimitives_.IdentityOperator));
            return true;
        }

        if (directSrgb && currentEncoding.TransferFunction.IsSrgb)
        {
            CompandingUtilities.BoundedOperator<SRgbCompanding.ExpandOperator> expand = new(new(1.055F, 0.055F, 0.04045F), PixelColorRange.Extended);
            RgbToXyb(in matrix, biases, cubeRootBiases, image, image, linear, expand);
            return true;
        }

        // General profile conversion is still supplied by the codec's color-management integration.
        JxlColorEncoding linearSrgb = new()
        {
            ColorSpace = currentEncoding.IsGray ? JxlColorSpace.Gray : JxlColorSpace.Rgb,
            TransferFunction = new() { TransferFunction = JxlTransferFunction.Linear }
        };

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

        // Read the actual transformed owner directly. When linear output is retained, no full-image copy back to image is needed.
        RgbToXyb(in matrix, biases, cubeRootBiases, linear ?? image, image, null, default(TensorPrimitives_.IdentityOperator));
        return true;
    }
}
