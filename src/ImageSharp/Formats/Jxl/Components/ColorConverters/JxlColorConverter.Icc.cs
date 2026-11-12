// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    private sealed partial class JxlColorConverter<TOperator>
    {
        /// <summary>
        /// Converts working RGB through the prepared destination profile and separates its device components from alpha.
        /// </summary>
        /// <param name="source">The borrowed working RGB row, already associated when required. No source component changes.</param>
        /// <param name="component0">The first encoded component row.</param>
        /// <param name="component1">The second component row, or empty for luminance.</param>
        /// <param name="component2">The third component row, or empty for luminance.</param>
        /// <param name="black">The remaining-light black output for CMYK, or empty for other models.</param>
        /// <param name="alpha">The output alpha row, or empty for opaque encoding.</param>
        /// <param name="profileConverter">The image's prepared sRGB-to-device transform.</param>
        /// <param name="scratch">Separate storage for RGB input and device output: three floats per pixel, or seven for CMYK.</param>
        private void ConvertFromIcc(
            ReadOnlySpan<Vector4> source,
            Span<float> component0,
            Span<float> component1,
            Span<float> component2,
            Span<float> black,
            Span<float> alpha,
            ColorProfileConverter profileConverter,
            Span<float> scratch)
        {
            int length = source.Length;
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(scratch[..(length * 3)]);
            Span<float> cmykSamples = TOperator.ComponentCount == 4 ? scratch.Slice(length * 3, length * 4) : [];

            // ICC device channels never contain pixel alpha. Pack only RGB into the borrowed scratch;
            // keep W in the unchanged source row until the final component traversal stores alpha separately.
            Rgb.FromScaledVector4(source, rgb);

            if (TOperator.ComponentCount == 1)
            {
                profileConverter.Convert<Rgb, Y>(rgb, MemoryMarshal.Cast<float, Y>(component0));
            }
            else if (TOperator.ComponentCount == 4)
            {
                // RGB-to-CMYK expands each pixel from three floats to four. Keep source and destination disjoint:
                // an in-place expansion would overwrite RGB values that the next conversion block still needs.
                profileConverter.Convert<Rgb, Cmyk>(rgb, MemoryMarshal.Cast<float, Cmyk>(cmykSamples));
            }
            else
            {
                // The shared ICC worker supports exact in-place RGB conversion. This row owns no extra buffers.
                profileConverter.Convert<Rgb, Rgb>(rgb, rgb);
            }

            TOperator operation = this.operation;
            ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<Rgb, Vector3>(rgb);
            ReadOnlySpan<Vector4> packedCmyk = MemoryMarshal.Cast<float, Vector4>(cmykSamples);
            ref float first = ref MemoryMarshal.GetReference(component0);
            ref float second = ref MemoryMarshal.GetReference(component1);
            ref float third = ref MemoryMarshal.GetReference(component2);
            ref float blackOutput = ref MemoryMarshal.GetReference(black);
            ref float opacity = ref MemoryMarshal.GetReference(alpha);
            bool hasAlpha = !alpha.IsEmpty;
            int i = 0;

            // RGB and CMYK use their typed packed layouts. Original RGBA remains separate and supplies W only.
            // Luminance already occupies its final first plane. Do not apply RGB luminance weights to that profile result.
            // The CMYK operator inverts ink amounts to remaining light. Black and alpha always use separate outputs.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint count = (length - i).Vector512Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out _, out _, out _, out Vector512<float> a);
                    Vector512<float> r;
                    Vector512<float> g = default;
                    Vector512<float> b = default;
                    Vector512<float> k = default;

                    if (TOperator.ComponentCount == 1)
                    {
                        r = Vector512.LoadUnsafe(ref first, (nuint)i);
                    }
                    else if (TOperator.ComponentCount == 4)
                    {
                        SimdUtils.Deinterleave4(packedCmyk, i, out r, out g, out b, out k);
                    }
                    else
                    {
                        SimdUtils.Deinterleave3(packed, i, out r, out g, out b);
                    }

                    if (TOperator.ComponentCount != 1)
                    {
                        operation.Invoke(ref r, ref g, ref b, ref k);
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);

                        if (TOperator.ComponentCount == 4)
                        {
                            k.StoreUnsafe(ref blackOutput, (nuint)i);
                        }
                    }

                    r.StoreUnsafe(ref first, (nuint)i);

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint count = (length - i).Vector256Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out _, out _, out _, out Vector256<float> a);
                    Vector256<float> r;
                    Vector256<float> g = default;
                    Vector256<float> b = default;
                    Vector256<float> k = default;

                    if (TOperator.ComponentCount == 1)
                    {
                        r = Vector256.LoadUnsafe(ref first, (nuint)i);
                    }
                    else if (TOperator.ComponentCount == 4)
                    {
                        SimdUtils.Deinterleave4(packedCmyk, i, out r, out g, out b, out k);
                    }
                    else
                    {
                        SimdUtils.Deinterleave3(packed, i, out r, out g, out b);
                    }

                    if (TOperator.ComponentCount != 1)
                    {
                        operation.Invoke(ref r, ref g, ref b, ref k);
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);

                        if (TOperator.ComponentCount == 4)
                        {
                            k.StoreUnsafe(ref blackOutput, (nuint)i);
                        }
                    }

                    r.StoreUnsafe(ref first, (nuint)i);

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint count = (length - i).Vector128Count<float>();

                for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
                {
                    SimdUtils.Deinterleave4(source, i, out _, out _, out _, out Vector128<float> a);
                    Vector128<float> r;
                    Vector128<float> g = default;
                    Vector128<float> b = default;
                    Vector128<float> k = default;

                    if (TOperator.ComponentCount == 1)
                    {
                        r = Vector128.LoadUnsafe(ref first, (nuint)i);
                    }
                    else if (TOperator.ComponentCount == 4)
                    {
                        SimdUtils.Deinterleave4(packedCmyk, i, out r, out g, out b, out k);
                    }
                    else
                    {
                        SimdUtils.Deinterleave3(packed, i, out r, out g, out b);
                    }

                    if (TOperator.ComponentCount != 1)
                    {
                        operation.Invoke(ref r, ref g, ref b, ref k);
                        g.StoreUnsafe(ref second, (nuint)i);
                        b.StoreUnsafe(ref third, (nuint)i);

                        if (TOperator.ComponentCount == 4)
                        {
                            k.StoreUnsafe(ref blackOutput, (nuint)i);
                        }
                    }

                    r.StoreUnsafe(ref first, (nuint)i);

                    if (hasAlpha)
                    {
                        a.StoreUnsafe(ref opacity, (nuint)i);
                    }
                }
            }

            // Only the unfinished tail reaches scalar conversion. Read alpha from the original pixel row,
            // not from profile scratch, so the fourth CMYK component cannot replace it.
            for (; i < length; i++)
            {
                float a = source[i].W;
                float r;
                float g = 0F;
                float b = 0F;
                float k = 0F;

                if (TOperator.ComponentCount == 1)
                {
                    r = component0[i];
                }
                else if (TOperator.ComponentCount == 4)
                {
                    Vector4 value = packedCmyk[i];
                    r = value.X;
                    g = value.Y;
                    b = value.Z;
                    k = value.W;
                }
                else
                {
                    Vector3 value = packed[i];
                    r = value.X;
                    g = value.Y;
                    b = value.Z;
                }

                if (TOperator.ComponentCount != 1)
                {
                    operation.Invoke(ref r, ref g, ref b, ref k);
                    component1[i] = g;
                    component2[i] = b;

                    if (TOperator.ComponentCount == 4)
                    {
                        black[i] = k;
                    }
                }

                component0[i] = r;

                if (hasAlpha)
                {
                    alpha[i] = a;
                }
            }
        }
    }
}
