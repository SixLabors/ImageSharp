// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Unassociates normalized RGB components before color-profile conversion.
    /// </summary>
    /// <param name="red">The associated red components.</param>
    /// <param name="green">The associated green components.</param>
    /// <param name="blue">The associated blue components.</param>
    /// <param name="alpha">The normalized auxiliary samples.</param>
    public static void UnassociateRgb(Span<float> red, Span<float> green, Span<float> blue, ReadOnlySpan<float> alpha)
    {
        ref float r = ref MemoryMarshal.GetReference(red);
        ref float g = ref MemoryMarshal.GetReference(green);
        ref float b = ref MemoryMarshal.GetReference(blue);
        ref float a = ref MemoryMarshal.GetReference(alpha);
        int x = 0;

        // Each lane is one pixel, with the same lane index in all four planes. Divide the
        // three color vectors by the alpha vector directly; no interleaving or lane broadcast
        // is needed. Zero alpha preserves hidden RGB, matching the shared unassociation contract.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= red.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> av = Vector512.LoadUnsafe(ref a, (nuint)x);
                Vector512<float> rv = Vector512.LoadUnsafe(ref r, (nuint)x);
                Vector512<float> gv = Vector512.LoadUnsafe(ref g, (nuint)x);
                Vector512<float> bv = Vector512.LoadUnsafe(ref b, (nuint)x);
                Vector512<float> zeroAlpha = Vector512.Equals(av, Vector512<float>.Zero);
                rv = Vector512.ConditionalSelect(zeroAlpha, rv, rv / av);
                gv = Vector512.ConditionalSelect(zeroAlpha, gv, gv / av);
                bv = Vector512.ConditionalSelect(zeroAlpha, bv, bv / av);
                Vector512.Clamp(rv, Vector512<float>.Zero, Vector512.Create(1F)).StoreUnsafe(ref r, (nuint)x);
                Vector512.Clamp(gv, Vector512<float>.Zero, Vector512.Create(1F)).StoreUnsafe(ref g, (nuint)x);
                Vector512.Clamp(bv, Vector512<float>.Zero, Vector512.Create(1F)).StoreUnsafe(ref b, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= red.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> av = Vector256.LoadUnsafe(ref a, (nuint)x);
                Vector256<float> rv = Vector256.LoadUnsafe(ref r, (nuint)x);
                Vector256<float> gv = Vector256.LoadUnsafe(ref g, (nuint)x);
                Vector256<float> bv = Vector256.LoadUnsafe(ref b, (nuint)x);
                Vector256<float> zeroAlpha = Vector256.Equals(av, Vector256<float>.Zero);
                rv = Vector256.ConditionalSelect(zeroAlpha, rv, rv / av);
                gv = Vector256.ConditionalSelect(zeroAlpha, gv, gv / av);
                bv = Vector256.ConditionalSelect(zeroAlpha, bv, bv / av);
                Vector256.Clamp(rv, Vector256<float>.Zero, Vector256.Create(1F)).StoreUnsafe(ref r, (nuint)x);
                Vector256.Clamp(gv, Vector256<float>.Zero, Vector256.Create(1F)).StoreUnsafe(ref g, (nuint)x);
                Vector256.Clamp(bv, Vector256<float>.Zero, Vector256.Create(1F)).StoreUnsafe(ref b, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= red.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> av = Vector128.LoadUnsafe(ref a, (nuint)x);
                Vector128<float> rv = Vector128.LoadUnsafe(ref r, (nuint)x);
                Vector128<float> gv = Vector128.LoadUnsafe(ref g, (nuint)x);
                Vector128<float> bv = Vector128.LoadUnsafe(ref b, (nuint)x);
                Vector128<float> zeroAlpha = Vector128.Equals(av, Vector128<float>.Zero);
                rv = Vector128.ConditionalSelect(zeroAlpha, rv, rv / av);
                gv = Vector128.ConditionalSelect(zeroAlpha, gv, gv / av);
                bv = Vector128.ConditionalSelect(zeroAlpha, bv, bv / av);
                Vector128.Clamp(rv, Vector128<float>.Zero, Vector128.Create(1F)).StoreUnsafe(ref r, (nuint)x);
                Vector128.Clamp(gv, Vector128<float>.Zero, Vector128.Create(1F)).StoreUnsafe(ref g, (nuint)x);
                Vector128.Clamp(bv, Vector128<float>.Zero, Vector128.Create(1F)).StoreUnsafe(ref b, (nuint)x);
            }
        }

        // Successively narrower vectors consume their own tails. The remaining zero to three
        // pixels use the same division and clamp without touching the auxiliary plane.
        for (; x < red.Length; x++)
        {
            float value = alpha[x];
            if (value != 0F)
            {
                red[x] /= value;
                green[x] /= value;
                blue[x] /= value;
            }

            red[x] = Math.Clamp(red[x], 0F, 1F);
            green[x] = Math.Clamp(green[x], 0F, 1F);
            blue[x] = Math.Clamp(blue[x], 0F, 1F);
        }
    }
}
