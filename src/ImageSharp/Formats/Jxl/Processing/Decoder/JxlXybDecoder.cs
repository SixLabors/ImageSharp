// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Decodes the XYB color format (which JPEG XL uses) into RGB.
/// </summary>
internal static class JxlXybDecoder
{
    /// <summary>
    /// Converts XYB to RGB using SIMD, one vector at a time.
    /// </summary>
    /// <param name="opsinX">X channel</param>
    /// <param name="opsinY">Y channel</param>
    /// <param name="opsinB">B channel</param>
    /// <param name="opsinParameters">Opsin parameters &amp; configuration</param>
    /// <param name="linearR">Output R</param>
    /// <param name="linearG">Output G</param>
    /// <param name="linearB">Output B</param>
    public static void ConvertXybToRgb(
        Vector<float> opsinX,
        Vector<float> opsinY,
        Vector<float> opsinB,
        JxlOpsinParameters opsinParameters,
        ref Vector<float> linearR,
        ref Vector<float> linearG,
        ref Vector<float> linearB)
    {
        Vector<float> negBiasR = new(opsinParameters.OpsinBiases[0]);
        Vector<float> negBiasG = new(opsinParameters.OpsinBiases[1]);
        Vector<float> negBiasB = new(opsinParameters.OpsinBiases[2]);

        Vector<float> gammaR = opsinX + opsinY;
        Vector<float> gammaG = opsinY - opsinX;
        Vector<float> gammaB = opsinB;

        Vector<float> gammaR2 = gammaR * gammaR;
        Vector<float> gammaG2 = gammaG * gammaG;
        Vector<float> gammaB2 = gammaB * gammaB;

        Vector<float> mixedR = (gammaR2 * gammaR) + negBiasR;
        Vector<float> mixedG = (gammaG2 * gammaG) + negBiasG;
        Vector<float> mixedB = (gammaB2 * gammaB) + negBiasB;

        Span<float> inverseMatrix = opsinParameters.InverseOpsinMatrix;

        linearR = LoadDuplicate128(ref inverseMatrix[0 * 4]) * mixedR;
        linearG = LoadDuplicate128(ref inverseMatrix[3 * 4]) * mixedR;
        linearB = LoadDuplicate128(ref inverseMatrix[6 * 4]) * mixedR;

        linearR = (LoadDuplicate128(ref inverseMatrix[1 * 4]) * mixedG) + linearR;
        linearG = (LoadDuplicate128(ref inverseMatrix[4 * 4]) * mixedG) + linearG;
        linearB = (LoadDuplicate128(ref inverseMatrix[7 * 4]) * mixedG) + linearB;

        linearR = (LoadDuplicate128(ref inverseMatrix[2 * 4]) * mixedB) + linearR;
        linearG = (LoadDuplicate128(ref inverseMatrix[5 * 4]) * mixedB) + linearG;
        linearB = (LoadDuplicate128(ref inverseMatrix[8 * 4]) * mixedB) + linearB;
    }

    public static bool OpsinToLinear(JxlImage3F opsin, Rectangle rect, JxlImage3F linear, JxlOpsinParameters opsinParameters)
    {
        if (!JxlImageOperations.SameSize(rect, linear))
        {
            return false;
        }

        if (Vector<float>.Count < 4)
        {
            // TODO: support 64bit vectors or no SIMD?
            throw new PlatformNotSupportedException("XYB to RGB conversion requires at least 128-bit SIMD");
        }

        // Reuse variables instead of creating them over
        // and over again
        Unsafe.SkipInit(out Vector<float> linearR);
        Unsafe.SkipInit(out Vector<float> linearG);
        Unsafe.SkipInit(out Vector<float> linearB);

        for (int y = 0; y < rect.Height; y++)
        {
            ReadOnlySpan<float> rowOpsin0 = opsin.PlaneRow(rect, 0, y);
            ReadOnlySpan<float> rowOpsin1 = opsin.PlaneRow(rect, 1, y);
            ReadOnlySpan<float> rowOpsin2 = opsin.PlaneRow(rect, 2, y);

            ref float rowOpsin0Reference = ref MemoryMarshal.GetReference(rowOpsin0);
            ref float rowOpsin1Reference = ref MemoryMarshal.GetReference(rowOpsin1);
            ref float rowOpsin2Reference = ref MemoryMarshal.GetReference(rowOpsin2);

            Span<float> rowLinear0 = linear.PlaneRow(0, y);
            Span<float> rowLinear1 = linear.PlaneRow(1, y);
            Span<float> rowLinear2 = linear.PlaneRow(2, y);

            ref float rowLinear0Reference = ref MemoryMarshal.GetReference(rowLinear0);
            ref float rowLinear1Reference = ref MemoryMarshal.GetReference(rowLinear1);
            ref float rowLinear2Reference = ref MemoryMarshal.GetReference(rowLinear2);

            for (int x = 0; x < rect.Height; x += Vector<float>.Count)
            {
                Vector<float> inOpsinX = Vector.LoadUnsafe(ref Unsafe.Add(ref rowOpsin0Reference, x));
                Vector<float> inOpsinY = Vector.LoadUnsafe(ref Unsafe.Add(ref rowOpsin1Reference, x));
                Vector<float> inOpsinB = Vector.LoadUnsafe(ref Unsafe.Add(ref rowOpsin2Reference, x));

                ConvertXybToRgb(inOpsinX, inOpsinY, inOpsinB, opsinParameters, ref linearR, ref linearG, ref linearB);

                linearR.StoreUnsafe(ref Unsafe.Add(ref rowLinear0Reference, x));
                linearG.StoreUnsafe(ref Unsafe.Add(ref rowLinear1Reference, x));
                linearB.StoreUnsafe(ref Unsafe.Add(ref rowLinear2Reference, x));
            }
        }

        return true;
    }

    /// <summary>
    /// A SIMD utility method which reads next 128 bits
    /// (which in this case happens to be next 4 floats),
    /// and duplicates them to fit in the CPU vector size.
    /// For example,
    ///
    ///     128 bit vectors: A B C D    (as-is)
    ///     256 bit vectors: A B C D A B C D (duplicate once)
    ///     512 bit vectors: A B C D A B C D A B C D A B C D (duplicate three times)
    ///
    /// Vector&lt;T&gt; has support for arbitrarily large
    /// vector sizes. For example, some ARM CPUs support 2048-bit
    /// vectors through Vector&lt;T&gt;. In that specific case, this
    /// method can be used for future-proofing.
    ///
    /// Note that this method, albeit future-proof, may be considered
    /// slow for smaller vector sizes (think CPUs with 128bit or 256bit vectors).
    /// </summary>
    /// <param name="reference">Reference to first element to load &amp; duplicate.</param>
    /// <returns>Vector with first 128 bits duplicated across the vector width.</returns>
    private static Vector<float> LoadDuplicate128(ref float reference)
    {
        Span<float> value = stackalloc float[Vector<float>.Count];
        Span<float> values128 = [
            reference,
            Unsafe.Add(ref reference, 1),
            Unsafe.Add(ref reference, 2),
            Unsafe.Add(ref reference, 3)
        ];

        for (int i = 0; i < Vector<float>.Count; i += 4)
        {
            values128[i..].CopyTo(value[i..]);
        }

        return new(value);
    }

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
