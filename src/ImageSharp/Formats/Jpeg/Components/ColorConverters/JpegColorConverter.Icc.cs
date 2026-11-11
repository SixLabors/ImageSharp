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
        /// <remarks>
        /// Each sample uses one <see cref="Vector4"/>: [Y Y Y 1] for one component, [C0 C1 C2 1] for three,
        /// and [C M Y K] for four. W is 1, the opaque JPEG alpha, if it does not hold K.
        /// Thus, the ICC transform reads the vectors without another model conversion.
        /// </remarks>
        /// <param name="source">The JPEG component planes.</param>
        /// <param name="destination">Caller-owned storage for one vector per sample.</param>
        /// <param name="profileSpace">The source profile's device space, fixed for the complete row.</param>
        private void UnpackProfileComponents(in ComponentValues source, Span<Vector4> destination, IccColorSpaceType profileSpace)
        {
            int channels = TOperator.ComponentCount;
            int count = source.Component0.Length;
            ref float c0 = ref MemoryMarshal.GetReference(source.Component0);
            ref float c1 = ref MemoryMarshal.GetReference(source.Component1);
            ref float c2 = ref MemoryMarshal.GetReference(source.Component2);
            ref float c3 = ref MemoryMarshal.GetReference(source.Component3);
            ref float packed = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            float scale = 1F / this.MaximumValue;
            float halfValue = this.HalfValue;
            int i = 0;

            // ComponentCount is constant for each closed converter. The JIT removes unused channel branches.
            // Empty component spans are never read. Each vector lane carries one pixel throughout the operator call.
            // The fourth register is K for four components. For other component counts, it is 1, the opaque alpha.
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
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);

                    if (channels == 1)
                    {
                        y = x;
                        z = x;
                    }

                    SimdUtils.Interleave4(x, y, z, channels == 4 ? k : Vector512<float>.One, ref Unsafe.Add(ref packed, (uint)i * 4));
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
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);

                    if (channels == 1)
                    {
                        y = x;
                        z = x;
                    }

                    SimdUtils.Interleave4(x, y, z, channels == 4 ? k : Vector256<float>.One, ref Unsafe.Add(ref packed, (uint)i * 4));
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
                    TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);

                    if (channels == 1)
                    {
                        y = x;
                        z = x;
                    }

                    if (channels != 4)
                    {
                        k = Vector128<float>.One;
                    }

                    // The method transposes four component registers into four component tuples. Then it stores the tuples in sequence.
                    SimdUtils.Transpose4(
                        x,
                        y,
                        z,
                        k,
                        out Vector128<float> pixel0,
                        out Vector128<float> pixel1,
                        out Vector128<float> pixel2,
                        out Vector128<float> pixel3);

                    nuint offset = (uint)i * 4;
                    pixel0.StoreUnsafe(ref packed, offset);
                    pixel1.StoreUnsafe(ref packed, offset + 4);
                    pixel2.StoreUnsafe(ref packed, offset + 8);
                    pixel3.StoreUnsafe(ref packed, offset + 12);
                }
            }

            // Finish with the same scalar operator. With SIMD disabled, this loop handles every pixel.
            ref Vector4 vectors = ref MemoryMarshal.GetReference(destination);

            for (; i < count; i++)
            {
                nuint offset = (uint)i;
                float x = Unsafe.Add(ref c0, offset);
                float y = channels >= 3 ? Unsafe.Add(ref c1, offset) : 0F;
                float z = channels >= 3 ? Unsafe.Add(ref c2, offset) : 0F;
                float k = channels == 4 ? Unsafe.Add(ref c3, offset) : 0F;
                TOperator.ConvertToProfile(ref x, ref y, ref z, ref k, this.MaximumValue, halfValue, scale, profileSpace);

                Unsafe.Add(ref vectors, offset) = channels switch
                {
                    1 => new Vector4(x, x, x, 1F),
                    3 => new Vector4(x, y, z, 1F),
                    _ => new Vector4(x, y, z, k),
                };
            }
        }

        /// <summary>
        /// Deinterleaves normalized profile samples into JPEG planes through the selected operator.
        /// </summary>
        /// <remarks>
        /// JPEG stores bounded integer components. The method clamps each profile component to [0,1] before the model conversion.
        /// A clamp of only YCbCr after the conversion can change the color when RGB is outside the target gamut.
        /// </remarks>
        /// <param name="source">The packed normalized profile components.</param>
        /// <param name="stride">
        /// The number of floats for each sample in <paramref name="source"/>: one, three or four.
        /// RGB samples in <see cref="Vector4"/> rows have a stride of four. The method ignores the fourth float of these samples.
        /// </param>
        /// <param name="destination">The destination JPEG component planes.</param>
        /// <param name="profileSpace">The target profile's device space, fixed for the complete row.</param>
        private void PackProfileComponents(ReadOnlySpan<float> source, int stride, in ComponentValues destination, IccColorSpaceType profileSpace)
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

            ReadOnlySpan<Vector3> triples = stride == 3 ? MemoryMarshal.Cast<float, Vector3>(source) : default;
            ReadOnlySpan<Vector4> quadruples = stride == 4 ? MemoryMarshal.Cast<float, Vector4>(source) : default;

            // ComponentCount is constant for each closed converter. The JIT removes unused channel branches.
            // The stride selects the load. The channel count selects the clamp and the stores.
            // The clamp uses ColorNumerics.ClampToFinite, as the span clamp of the shared ICC path does.
            // It sets NaN to 0. It keeps values in [0,1] without a change, also -0.
            // With a stride of four and three channels, k holds alpha. The operators do not read k.
            // The method stores k only for four channels. Thus, alpha has no effect on the output.
            // Empty component spans are never read. Each vector lane carries one pixel throughout the operator call.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> maximum = Vector512.Create(this.MaximumValue);
                Vector512<float> half = Vector512.Create(halfValue);
                Vector512<float> reciprocal = Vector512.Create(scale);
                nuint vectorCount = source[(i * stride)..].Vector512Count<float>() / (uint)stride;

                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x;
                    Vector512<float> y = default;
                    Vector512<float> z = default;
                    Vector512<float> k = default;

                    if (stride == 1)
                    {
                        x = Vector512.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (stride == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        SimdUtils.Deinterleave4(quadruples, i, out x, out y, out z, out k);
                    }

                    // Clamp the profile components before the model conversion.
                    x = ColorNumerics.ClampToFinite(x, Vector512<float>.Zero, Vector512<float>.One);
                    if (channels >= 3)
                    {
                        y = ColorNumerics.ClampToFinite(y, Vector512<float>.Zero, Vector512<float>.One);
                        z = ColorNumerics.ClampToFinite(z, Vector512<float>.Zero, Vector512<float>.One);
                    }

                    if (channels == 4)
                    {
                        k = ColorNumerics.ClampToFinite(k, Vector512<float>.Zero, Vector512<float>.One);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);
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
                nuint vectorCount = source[(i * stride)..].Vector256Count<float>() / (uint)stride;

                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x;
                    Vector256<float> y = default;
                    Vector256<float> z = default;
                    Vector256<float> k = default;

                    if (stride == 1)
                    {
                        x = Vector256.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (stride == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        SimdUtils.Deinterleave4(quadruples, i, out x, out y, out z, out k);
                    }

                    // Clamp the profile components before the model conversion.
                    x = ColorNumerics.ClampToFinite(x, Vector256<float>.Zero, Vector256<float>.One);
                    if (channels >= 3)
                    {
                        y = ColorNumerics.ClampToFinite(y, Vector256<float>.Zero, Vector256<float>.One);
                        z = ColorNumerics.ClampToFinite(z, Vector256<float>.Zero, Vector256<float>.One);
                    }

                    if (channels == 4)
                    {
                        k = ColorNumerics.ClampToFinite(k, Vector256<float>.Zero, Vector256<float>.One);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);
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
                nuint vectorCount = source[(i * stride)..].Vector128Count<float>() / (uint)stride;

                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x;
                    Vector128<float> y = default;
                    Vector128<float> z = default;
                    Vector128<float> k = default;

                    if (stride == 1)
                    {
                        x = Vector128.LoadUnsafe(ref packed, (nuint)i);
                    }
                    else if (stride == 3)
                    {
                        SimdUtils.Deinterleave3(triples, i, out x, out y, out z);
                    }
                    else
                    {
                        // Four tuples of four floats become one register per component. Read the full group before storing.
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

                    // Clamp the profile components before the model conversion.
                    x = ColorNumerics.ClampToFinite(x, Vector128<float>.Zero, Vector128<float>.One);
                    if (channels >= 3)
                    {
                        y = ColorNumerics.ClampToFinite(y, Vector128<float>.Zero, Vector128<float>.One);
                        z = ColorNumerics.ClampToFinite(z, Vector128<float>.Zero, Vector128<float>.One);
                    }

                    if (channels == 4)
                    {
                        k = ColorNumerics.ClampToFinite(k, Vector128<float>.Zero, Vector128<float>.One);
                    }

                    TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, maximum, half, reciprocal, profileSpace);
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
                nuint packedOffset = offset * (uint)stride;

                // The scalar clamp has the same rules as the vector clamp.
                float x = ColorNumerics.ClampToFinite(Unsafe.Add(ref packed, packedOffset), 0F, 1F);
                float y = channels >= 3 ? ColorNumerics.ClampToFinite(Unsafe.Add(ref packed, packedOffset + 1), 0F, 1F) : 0F;
                float z = channels >= 3 ? ColorNumerics.ClampToFinite(Unsafe.Add(ref packed, packedOffset + 2), 0F, 1F) : 0F;
                float k = channels == 4 ? ColorNumerics.ClampToFinite(Unsafe.Add(ref packed, packedOffset + 3), 0F, 1F) : 0F;
                TOperator.ConvertFromProfile(ref x, ref y, ref z, ref k, this.MaximumValue, halfValue, scale, profileSpace);
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
        public override void ConvertToRgb(in ComponentValues source, Span<Vector4> destination, ColorProfileConverter profileConverter)
        {
            Span<Vector4> vectors = destination[..source.Component0.Length];
            IccColorSpaceType profileSpace = profileConverter.Options.SourceIccProfile.Header.DataColorSpace;

            // Apply scaling, inversion and the profile's model in the same traversal that interleaves the planes.
            // The vectors have the scaled layout of the color model. Thus, the ICC operator reads them directly.
            this.UnpackProfileComponents(source, vectors, profileSpace);

            ColorProfileConverterExtensionsIcc.IccTransform transform = profileConverter.GetIccTransform();
            transform.Converter.Convert(vectors, vectors, transform.ConvertsW);

            // A four-channel profile reads W as K, and the result has no alpha. JPEG pixels are opaque.
            // Thus, the method sets W after the transform.
            // For other profiles, W is not color data. The transform keeps the opaque W from the unpack step.
            if (transform.ConvertsW)
            {
                for (int i = 0; i < vectors.Length; i++)
                {
                    vectors[i].W = 1F;
                }
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(
            Span<Vector4> source,
            in ComponentValues destination,
            ColorProfileConverter profileConverter,
            Span<Rgb> scratch)
        {
            IccColorSpaceType profileSpace = profileConverter.Options.TargetIccProfile.Header.DataColorSpace;
            bool rgbProfile = TOperator.ColorSpace switch
            {
                JpegColorSpace.Luminance or JpegColorSpace.Cmyk or JpegColorSpace.TiffCmyk or JpegColorSpace.Ycck or JpegColorSpace.TiffYccK => false,
                JpegColorSpace.YCbCr => profileSpace != IccColorSpaceType.YCbCr,
                _ => true
            };

            if (rgbProfile)
            {
                // An RGB profile converts the vectors in place. W is alpha and does not enter the transform.
                // The operator gets W = 1, the value that Rgb.ToScaledVector4 gives. The copies to and from Rgb do not change R, G or B.
                // Thus, the result is the same as a conversion through Rgb storage.
                // The packer reads R, G and B from each vector and ignores W. Thus, no copy to Rgb is necessary.
                profileConverter.Convert(source);
                this.PackProfileComponents(MemoryMarshal.Cast<Vector4, float>(source), 4, destination, profileSpace);
                return;
            }

            // Other profiles do not give RGB. The shared model conversion reads Rgb and writes the profile components.
            // The vectors are not necessary after the copy to Rgb. Thus, the components use the same storage.
            int channels = TOperator.ComponentCount;
            Span<Rgb> rgb = scratch[..source.Length];
            Rgb.FromScaledVector4(source, rgb);
            Span<float> samples = MemoryMarshal.Cast<Vector4, float>(source)[..(source.Length * channels)];

            switch (TOperator.ColorSpace)
            {
                case JpegColorSpace.Luminance:
                    profileConverter.Convert<Rgb, Y>(rgb, MemoryMarshal.Cast<float, Y>(samples));
                    break;

                case JpegColorSpace.YCbCr:
                    profileConverter.Convert<Rgb, YCbCr>(rgb, MemoryMarshal.Cast<float, YCbCr>(samples));
                    break;

                default:
                    profileConverter.Convert<Rgb, Cmyk>(rgb, MemoryMarshal.Cast<float, Cmyk>(samples));
                    break;
            }

            // The matching operator applies the model transform, inversion and JPEG scaling after ICC has produced device samples.
            this.PackProfileComponents(samples, channels, destination, profileSpace);
        }
    }
}
