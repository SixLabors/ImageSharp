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
    }
}
