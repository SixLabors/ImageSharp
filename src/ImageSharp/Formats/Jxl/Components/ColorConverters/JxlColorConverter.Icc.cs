// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    private sealed partial class JxlColorConverter<TOperator>
    {
        /// <inheritdoc/>
        public override void ConvertToRgb<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<float> storage,
            ColorProfileConverter profileConverter,
            in TSampleOperator sampleOperator)
        {
            // Retain one local copy so constrained calls do not copy the readonly operation per block.
            TOperator operation = this.operation;
            TSampleOperator sampleOperation = sampleOperator;

            // Reconstructed planes stay fixed during conversion. Retain their spans locally so stores
            // and constrained calls cannot force the JIT to reload the borrowed descriptor in each block.
            ReadOnlySpan<TSample> component0 = source.Component0;
            ReadOnlySpan<TSample> component1 = source.Component1;
            ReadOnlySpan<TSample> component2 = source.Component2;
            int count = component0.Length;
            int channels = TOperator.ComponentCount == 4 ? 4
                : profileConverter.Options.SourceIccProfile is { Header.DataColorSpace: IccColorSpaceType.Gray } ? 1 : 3;

            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[..(3 * count)]);

            // RGB output occupies the first three floats per pixel. A gray input expands to RGB,
            // so place its one-component row after that output. RGB and CMYK can use the prefix:
            // their typed conversion keeps or reduces its size and loads each source block before writing.
            Span<float> samples = channels == 1 ? storage.Slice(3 * count, count) : storage[..(channels * count)];
            ref float black = ref MemoryMarshal.GetReference(source.Black);
            ref float target = ref MemoryMarshal.GetReference(samples);
            int i = 0;

            // Each lane holds one pixel while normalization and the coding transform run. Interleave
            // only the components belonging to Y, Rgb or Cmyk; image alpha never enters these profiles.
            // Short loads fill the low half of the register before the sample operator widens them.
            // The channel count is invariant for the row and every scalar/SIMD path uses the same layout.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector512Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x = sampleOperation.Invoke(JxlSampleConverter.Load512(component0, i));
                    Vector512<float> y = sampleOperation.Invoke(JxlSampleConverter.Load512(component1, i));
                    Vector512<float> z = sampleOperation.Invoke(JxlSampleConverter.Load512(component2, i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref target, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref target, offset));
                    }
                    else
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, offset));
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = sampleOperation.Invoke(JxlSampleConverter.Load256(component0, i));
                    Vector256<float> y = sampleOperation.Invoke(JxlSampleConverter.Load256(component1, i));
                    Vector256<float> z = sampleOperation.Invoke(JxlSampleConverter.Load256(component2, i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref target, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref target, offset));
                    }
                    else
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, offset));
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = sampleOperation.Invoke(JxlSampleConverter.Load128(component0, i));
                    Vector128<float> y = sampleOperation.Invoke(JxlSampleConverter.Load128(component1, i));
                    Vector128<float> z = sampleOperation.Invoke(JxlSampleConverter.Load128(component2, i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref target, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref target, offset));
                    }
                    else
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, offset));
                    }
                }
            }

            // The scalar remainder uses the same operator and sample normalization as every vector width.
            for (; i < count; i++)
            {
                float x = sampleOperation.Invoke(component0[i]);
                float y = sampleOperation.Invoke(component1[i]);
                float z = sampleOperation.Invoke(component2[i]);
                float k = TOperator.ComponentCount == 4 ? source.Black[i] : 0F;
                operation.Invoke(ref x, ref y, ref z, ref k);
                int offset = i * channels;
                samples[offset] = x;

                if (channels >= 3)
                {
                    samples[offset + 1] = y;
                    samples[offset + 2] = z;
                    if (channels == 4)
                    {
                        samples[offset + 3] = k;
                    }
                }
            }

            // The device type determines the source component count and encoding. ICC retains
            // fractional RGB; destination pixel packing follows after conversion, with alpha kept separate.
            if (channels == 1)
            {
                profileConverter.Convert<Y, Rgb>(MemoryMarshal.Cast<float, Y>(samples), rgb);
            }
            else if (channels == 4)
            {
                profileConverter.Convert<Cmyk, Rgb>(MemoryMarshal.Cast<float, Cmyk>(samples), rgb);
            }
            else
            {
                profileConverter.Convert<Rgb, Rgb>(MemoryMarshal.Cast<float, Rgb>(samples), rgb);
            }
        }

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
