// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed partial class JpegColorConverter<TOperator>
    {
        /// <summary>
        /// Interleaves JPEG planes into normalized profile samples through the selected operator.
        /// </summary>
        /// <param name="source">The JPEG component planes.</param>
        /// <param name="destination">Caller-owned packed profile storage.</param>
        private void UnpackProfileComponents(in ComponentValues source, Span<float> destination)
        {
            int channels = TOperator.ComponentCount;
            int count = source.Component0.Length;
            ref float c0 = ref MemoryMarshal.GetReference(source.Component0);
            ref float c1 = ref MemoryMarshal.GetReference(source.Component1);
            ref float c2 = ref MemoryMarshal.GetReference(source.Component2);
            ref float c3 = ref MemoryMarshal.GetReference(source.Component3);
            ref float packed = ref MemoryMarshal.GetReference(destination);
            float scale = 1F / this.MaximumValue;
            float halfValue = this.HalfValue;
            int i = 0;

            // ComponentCount is constant for each closed converter. The JIT removes unused channel branches.
            // Empty component spans are never read. Each vector lane carries one pixel throughout the operator call.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> maximum = Vector512.Create(this.MaximumValue);
                Vector512<float> half = Vector512.Create(halfValue);
                Vector512<float> reciprocal = Vector512.Create(scale);
                nuint vectorCount = source.Component0[i..].Vector512Count<float>();

                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x = Vector512.LoadUnsafe(ref c0, (nuint)i);
                    Vector512<float> y = channels >= 3 ? Vector512.LoadUnsafe(ref c1, (nuint)i) : default;
                    Vector512<float> z = channels >= 3 ? Vector512.LoadUnsafe(ref c2, (nuint)i) : default;
                    Vector512<float> k = channels == 4 ? Vector512.LoadUnsafe(ref c3, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref packed, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref packed, offset));
                    }
                    else
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref packed, offset));
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> maximum = Vector256.Create(this.MaximumValue);
                Vector256<float> half = Vector256.Create(halfValue);
                Vector256<float> reciprocal = Vector256.Create(scale);
                nuint vectorCount = source.Component0[i..].Vector256Count<float>();

                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = Vector256.LoadUnsafe(ref c0, (nuint)i);
                    Vector256<float> y = channels >= 3 ? Vector256.LoadUnsafe(ref c1, (nuint)i) : default;
                    Vector256<float> z = channels >= 3 ? Vector256.LoadUnsafe(ref c2, (nuint)i) : default;
                    Vector256<float> k = channels == 4 ? Vector256.LoadUnsafe(ref c3, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref packed, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref packed, offset));
                    }
                    else
                    {
                        SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref packed, offset));
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> maximum = Vector128.Create(this.MaximumValue);
                Vector128<float> half = Vector128.Create(halfValue);
                Vector128<float> reciprocal = Vector128.Create(scale);
                nuint vectorCount = source.Component0[i..].Vector128Count<float>();

                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = Vector128.LoadUnsafe(ref c0, (nuint)i);
                    Vector128<float> y = channels >= 3 ? Vector128.LoadUnsafe(ref c1, (nuint)i) : default;
                    Vector128<float> z = channels >= 3 ? Vector128.LoadUnsafe(ref c2, (nuint)i) : default;
                    Vector128<float> k = channels == 4 ? Vector128.LoadUnsafe(ref c3, (nuint)i) : default;
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    nuint offset = (uint)i * (uint)channels;

                    if (channels == 1)
                    {
                        x.StoreUnsafe(ref packed, offset);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Interleave3(x, y, z, ref Unsafe.Add(ref packed, offset));
                    }
                    else
                    {
                        // Transpose four component registers into four [C M Y K] tuples, then store them contiguously.
                        SimdUtils.Transpose4(
                            x,
                            y,
                            z,
                            k,
                            out Vector128<float> pixel0,
                            out Vector128<float> pixel1,
                            out Vector128<float> pixel2,
                            out Vector128<float> pixel3);

                        pixel0.StoreUnsafe(ref packed, offset);
                        pixel1.StoreUnsafe(ref packed, offset + 4);
                        pixel2.StoreUnsafe(ref packed, offset + 8);
                        pixel3.StoreUnsafe(ref packed, offset + 12);
                    }
                }
            }

            // Finish with the same scalar operator. With SIMD disabled, this loop handles every pixel.
            for (; i < count; i++)
            {
                nuint offset = (uint)i;
                nuint packedOffset = offset * (uint)channels;
                float x = Unsafe.Add(ref c0, offset);
                float y = channels >= 3 ? Unsafe.Add(ref c1, offset) : 0F;
                float z = channels >= 3 ? Unsafe.Add(ref c2, offset) : 0F;
                float k = channels == 4 ? Unsafe.Add(ref c3, offset) : 0F;
                TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, this.MaximumValue, halfValue, scale);
                Unsafe.Add(ref packed, packedOffset) = x;

                if (channels >= 3)
                {
                    Unsafe.Add(ref packed, packedOffset + 1) = y;
                    Unsafe.Add(ref packed, packedOffset + 2) = z;
                }

                if (channels == 4)
                {
                    Unsafe.Add(ref packed, packedOffset + 3) = k;
                }
            }
        }

        /// <summary>
        /// Deinterleaves normalized profile samples into JPEG planes through the selected operator.
        /// </summary>
        /// <param name="source">The packed normalized profile components.</param>
        /// <param name="destination">The destination JPEG component planes.</param>
        private void PackProfileComponents(ReadOnlySpan<float> source, in ComponentValues destination)
        {
            int channels = TOperator.ComponentCount;
            int count = destination.Component0.Length;
            ref float c0 = ref MemoryMarshal.GetReference(destination.Component0);
            ref float c1 = ref MemoryMarshal.GetReference(destination.Component1);
            ref float c2 = ref MemoryMarshal.GetReference(destination.Component2);
            ref float c3 = ref MemoryMarshal.GetReference(destination.Component3);
            ref float packed = ref MemoryMarshal.GetReference(source);
            float scale = 1F / this.MaximumValue;
            float halfValue = TOperator.GetProfileChromaOffset(this.MaximumValue, this.HalfValue);
            int i = 0;

            ReadOnlySpan<Vector3> triples = channels == 3 ? MemoryMarshal.Cast<float, Vector3>(source) : default;
            ReadOnlySpan<Vector4> quadruples = channels == 4 ? MemoryMarshal.Cast<float, Vector4>(source) : default;

            // ComponentCount is constant for each closed converter. The JIT removes unused channel branches.
            // Empty component spans are never read. Each vector lane carries one pixel throughout the operator call.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> maximum = Vector512.Create(this.MaximumValue);
                Vector512<float> half = Vector512.Create(halfValue);
                Vector512<float> reciprocal = Vector512.Create(scale);
                nuint vectorCount = source[(i * channels)..].Vector512Count<float>() / (uint)channels;

                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x;
                    Vector512<float> y = default;
                    Vector512<float> z = default;
                    Vector512<float> k = default;

                    if (channels == 1)
                    {
                        x = Vector512.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        SimdUtils.Deinterleave4(quadruples, i, out x, out y, out z, out k);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    x.StoreUnsafe(ref c0, (nuint)i);

                    if (channels >= 3)
                    {
                        y.StoreUnsafe(ref c1, (nuint)i);
                        z.StoreUnsafe(ref c2, (nuint)i);
                    }

                    if (channels == 4)
                    {
                        k.StoreUnsafe(ref c3, (nuint)i);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> maximum = Vector256.Create(this.MaximumValue);
                Vector256<float> half = Vector256.Create(halfValue);
                Vector256<float> reciprocal = Vector256.Create(scale);
                nuint vectorCount = source[(i * channels)..].Vector256Count<float>() / (uint)channels;

                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x;
                    Vector256<float> y = default;
                    Vector256<float> z = default;
                    Vector256<float> k = default;

                    if (channels == 1)
                    {
                        x = Vector256.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        SimdUtils.Deinterleave4(quadruples, i, out x, out y, out z, out k);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    x.StoreUnsafe(ref c0, (nuint)i);

                    if (channels >= 3)
                    {
                        y.StoreUnsafe(ref c1, (nuint)i);
                        z.StoreUnsafe(ref c2, (nuint)i);
                    }

                    if (channels == 4)
                    {
                        k.StoreUnsafe(ref c3, (nuint)i);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> maximum = Vector128.Create(this.MaximumValue);
                Vector128<float> half = Vector128.Create(halfValue);
                Vector128<float> reciprocal = Vector128.Create(scale);
                nuint vectorCount = source[(i * channels)..].Vector128Count<float>() / (uint)channels;

                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x;
                    Vector128<float> y = default;
                    Vector128<float> z = default;
                    Vector128<float> k = default;

                    if (channels == 1)
                    {
                        x = Vector128.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (channels == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        // Four [C M Y K] tuples become one register per component. Read the full group before storing.
                        nuint offset = (uint)i * 4;
                        SimdUtils.Transpose4(
                            Vector128.LoadUnsafe(ref packed, offset),
                            Vector128.LoadUnsafe(ref packed, offset + 4),
                            Vector128.LoadUnsafe(ref packed, offset + 8),
                            Vector128.LoadUnsafe(ref packed, offset + 12),
                            out x,
                            out y,
                            out z,
                            out k);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal);
                    x.StoreUnsafe(ref c0, (nuint)i);

                    if (channels >= 3)
                    {
                        y.StoreUnsafe(ref c1, (nuint)i);
                        z.StoreUnsafe(ref c2, (nuint)i);
                    }

                    if (channels == 4)
                    {
                        k.StoreUnsafe(ref c3, (nuint)i);
                    }
                }
            }

            // Finish with the same scalar operator. With SIMD disabled, this loop handles every pixel.
            for (; i < count; i++)
            {
                nuint offset = (uint)i;
                nuint packedOffset = offset * (uint)channels;
                float x = Unsafe.Add(ref packed, packedOffset);
                float y = channels >= 3 ? Unsafe.Add(ref packed, packedOffset + 1) : 0F;
                float z = channels >= 3 ? Unsafe.Add(ref packed, packedOffset + 2) : 0F;
                float k = channels == 4 ? Unsafe.Add(ref packed, packedOffset + 3) : 0F;
                TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, this.MaximumValue, halfValue, scale);
                Unsafe.Add(ref c0, offset) = x;

                if (channels >= 3)
                {
                    Unsafe.Add(ref c1, offset) = y;
                    Unsafe.Add(ref c2, offset) = z;
                }

                if (channels == 4)
                {
                    Unsafe.Add(ref c3, offset) = k;
                }
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgb(in ComponentValues source, Span<Rgb> destination, ColorProfileConverter profileConverter, Span<float> scratch)
        {
            int length = source.Component0.Length;

            // Grayscale expands from one component to three. Keep its source in the spare final row segment,
            // beyond the RGB destination, so packing one block cannot overwrite the next block's luminance.
            Span<float> samples = TOperator.ComponentCount == 1 ? scratch.Slice(length * 3, length) : scratch[..(length * TOperator.ComponentCount)];
            IccColorSpaceType profileSpace = profileConverter.Options.SourceIccProfile.Header.DataColorSpace;

            // The operator removes JPEG scaling, inversion and model transforms before the profile reads its device samples.
            this.UnpackProfileComponents(source, samples);
            TOperator.ConvertToProfile(samples, profileSpace);

            switch (TOperator.ColorSpace)
            {
                case JpegColorSpace.Grayscale:
                    profileConverter.Convert<Y, Rgb>(MemoryMarshal.Cast<float, Y>(samples), destination);
                    break;

                case JpegColorSpace.Cmyk:
                case JpegColorSpace.TiffCmyk:
                case JpegColorSpace.Ycck:
                case JpegColorSpace.TiffYccK:
                    profileConverter.Convert<Cmyk, Rgb>(MemoryMarshal.Cast<float, Cmyk>(samples), destination);
                    break;

                case JpegColorSpace.YCbCr when profileSpace == IccColorSpaceType.YCbCr:
                    profileConverter.Convert<YCbCr, Rgb>(MemoryMarshal.Cast<float, YCbCr>(samples), destination);
                    break;

                default:
                    profileConverter.Convert<Rgb, Rgb>(MemoryMarshal.Cast<float, Rgb>(samples), destination);
                    break;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(
            ReadOnlySpan<Rgb> source,
            in ComponentValues destination,
            ColorProfileConverter profileConverter,
            Span<float> scratch)
        {
            Span<float> samples = scratch[..(source.Length * TOperator.ComponentCount)];
            IccColorSpaceType profileSpace = profileConverter.Options.TargetIccProfile.Header.DataColorSpace;

            switch (TOperator.ColorSpace)
            {
                case JpegColorSpace.Grayscale:
                    profileConverter.Convert<Rgb, Y>(source, MemoryMarshal.Cast<float, Y>(samples));
                    break;

                case JpegColorSpace.Cmyk:
                case JpegColorSpace.TiffCmyk:
                case JpegColorSpace.Ycck:
                case JpegColorSpace.TiffYccK:
                    profileConverter.Convert<Rgb, Cmyk>(source, MemoryMarshal.Cast<float, Cmyk>(samples));
                    break;

                case JpegColorSpace.YCbCr when profileSpace == IccColorSpaceType.YCbCr:
                    profileConverter.Convert<Rgb, YCbCr>(source, MemoryMarshal.Cast<float, YCbCr>(samples));
                    break;

                default:
                    profileConverter.Convert<Rgb, Rgb>(source, MemoryMarshal.Cast<float, Rgb>(samples));
                    break;
            }

            // JPEG stores bounded integer components. Limit the profile's device samples before model conversion;
            // clipping only YCbCr afterward can change the color when RGB is outside the target gamut.
            // Shared ICC conversion stays unclipped for floating-point destinations such as RgbaVector.
            Numerics.ClampNumber(samples, 0F, 1F);

            // The matching operator applies the model transform, inversion and JPEG scaling after ICC has produced device samples.
            TOperator.ConvertFromProfile(samples, profileSpace);
            this.PackProfileComponents(samples, destination);
        }
    }
}
