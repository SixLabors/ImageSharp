// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows conversion between two color profiles based on the RGB color space.
/// </summary>
public static class ColorProfileConverterExtensionsRgbRgb
{
    /// <summary>
    /// Converts a color value from one color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// The conversion process may use ICC profiles if available; otherwise, it performs a manual
    /// conversion through the profile connection space (PCS) with chromatic adaptation as needed. The method requires
    /// both source and target types to be value types implementing the appropriate color profile interface.
    /// </remarks>
    /// <typeparam name="TFrom">The source color profile type. Must implement <see cref="IColorProfile{TFrom, Rgb}"/>.</typeparam>
    /// <typeparam name="TTo">The target color profile type. Must implement <see cref="IColorProfile{TTo, Rgb}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion.</param>
    /// <param name="source">The source color value to convert.</param>
    /// <returns>A value of type <typeparamref name="TTo"/> representing the converted color in the target color profile.</returns>
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        if (converter.ShouldUseIccProfiles())
        {
            return converter.ConvertUsingIccProfile<TFrom, TTo>(source);
        }

        ColorConversionOptions options = converter.Options;

        // A scalar call uses the existing prepared matrices directly. Do not prepare a bulk operation per color.
        Rgb input = source.ToProfileConnectingSpace(options);
        CieXyz xyz = input.ToProfileConnectingSpace(options);
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        xyz = VonKriesChromaticAdaptation.Transform(in xyz, whitePoints, options.AdaptationMatrix, options.InverseAdaptationMatrix);
        Rgb output = Rgb.FromProfileConnectingSpace(options, in xyz);
        return TTo.FromProfileConnectingSpace(options, in output);
    }

    /// <summary>
    /// Converts a span of color values from one color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// This method performs color conversion between two color profiles, handling necessary
    /// transformations such as profile connection space conversion and chromatic adaptation. If ICC profiles are
    /// available and applicable, the conversion uses them for improved accuracy. The method does not allocate memory
    /// for the destination; the caller is responsible for providing a suitably sized span.
    /// </remarks>
    /// <typeparam name="TFrom">The type representing the source color profile. Must implement <see cref="IColorProfile{TFrom, Rgb}"/>.</typeparam>
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo, Rgb}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion operation.</param>
    /// <param name="source">A read-only span containing the source color values to convert.</param>
    /// <param name="destination">A span that receives the converted color values. Must be at least as long as the source span.</param>
    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        if (converter.ShouldUseIccProfiles())
        {
            converter.ConvertUsingIccProfile(source, destination);
            return;
        }

        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<Rgb> pcsFromToOwner = options.MemoryAllocator.Allocate<Rgb>(source.Length);
        Span<Rgb> pcsFromTo = pcsFromToOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFromTo);

        RgbConversion operation = new(
            options.SourceRgbWorkingSpace,
            options.TargetRgbWorkingSpace,
            converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>(),
            options.AdaptationMatrix,
            options.InverseAdaptationMatrix);

        // Retain the complete source staging before any destination write. Models can have different
        // sizes and callers can reuse the same storage. The shared operation keeps XYZ and LMS in registers.
        operation.Invoke(pcsFromTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsFromTo, destination);
    }

    /// <summary>
    /// Converts between RGB working spaces with the established matrix stages and transfer APIs.
    /// </summary>
    /// <remarks>
    /// The generic RGB conversion family and codec component conversion share this operation. Its state
    /// remains private, so callers cannot pair adaptation coefficients with different working spaces.
    /// Construction uses the white points selected by the existing profile contracts, including custom profiles.
    /// </remarks>
    internal readonly struct RgbConversion
    {
        private readonly RgbWorkingSpace source;
        private readonly RgbWorkingSpace target;
        private readonly Matrix4x4 adaptationMatrix;
        private readonly Matrix4x4 inverseAdaptationMatrix;
        private readonly Vector3 scale;
        private readonly bool adapt;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbConversion"/> struct.
        /// </summary>
        /// <param name="source">The source primaries and transfer curve.</param>
        /// <param name="target">The target primaries and transfer curve.</param>
        /// <param name="whitePoints">The white points selected by the source and target profile contracts.</param>
        /// <param name="adaptationMatrix">The chromatic adaptation matrix.</param>
        /// <param name="inverseAdaptationMatrix">The existing inverse of the adaptation matrix.</param>
        public RgbConversion(
            RgbWorkingSpace source,
            RgbWorkingSpace target,
            (CieXyz From, CieXyz To) whitePoints,
            in Matrix4x4 adaptationMatrix,
            in Matrix4x4 inverseAdaptationMatrix)
        {
            this.source = source;
            this.target = target;
            this.adaptationMatrix = adaptationMatrix;
            this.inverseAdaptationMatrix = inverseAdaptationMatrix;
            this.adapt = !whitePoints.From.Equals(whitePoints.To);

            // Retain the established adaptation stages and their float rounding. Composing translated
            // matrices can discard small coefficients when large offsets cancel. Only the invariant white
            // response ratio is prepared here; no per-color matrix preparation or XYZ buffer is required.
            if (!this.adapt)
            {
                this.scale = Vector3.One;
                return;
            }

            Vector3 from = Vector3.Transform(whitePoints.From.AsVector3Unsafe(), adaptationMatrix);
            Vector3 to = Vector3.Transform(whitePoints.To.AsVector3Unsafe(), adaptationMatrix);
            this.scale = to / from;
        }

        /// <summary>
        /// Converts the existing packed RGB staging span without allocating another connection-space buffer.
        /// </summary>
        /// <param name="colors">The caller-owned RGB staging colors, converted in place.</param>
        public void Invoke(Span<Rgb> colors)
        {
            // Adapt packed storage with the existing RGB/vector bulk APIs. The stack block spans one widest
            // component register; every color is written before use and no storage is rented inside this loop.
            InlineArray16<Vector4> storage = default;
            Span<Vector4> buffer = storage;
            for (int offset = 0; offset < colors.Length; offset += buffer.Length)
            {
                int count = Math.Min(buffer.Length, colors.Length - offset);
                Span<Vector4> block = buffer[..count];
                Rgb.ToScaledVector4(colors.Slice(offset, count), block);
                this.Invoke(block);
                Rgb.FromScaledVector4(block, colors.Slice(offset, count));
            }
        }

        /// <summary>
        /// Converts an existing pixel-vector row in place and retains alpha.
        /// </summary>
        /// <param name="colors">The caller-owned source colors, replaced by target colors.</param>
        public void Invoke(Span<Vector4> colors)
        {
            // Sixteen colors complete one Vector512 component group and all narrower groups. The existing
            // transfer implementations process each in-place block; no per-row allocation or matrix setup occurs.
            Matrix4x4 sourceMatrix = this.source.RgbToCieXyzMatrix;
            Matrix4x4 targetMatrix = this.target.CieXyzToRgbMatrix;
            for (int offset = 0; offset < colors.Length; offset += Vector512<float>.Count)
            {
                Span<Vector4> block = colors.Slice(offset, Math.Min(Vector512<float>.Count, colors.Length - offset));
                this.source.Expand(block);
                ref float first = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(block));
                int i = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    nuint count = (block.Length - i).Vector512Count<float>();
                    for (nuint group = 0; group < count; group++, i += Vector512<float>.Count)
                    {
                        // Keep all connection-space stages in component registers. Each lane is one color;
                        // alpha bypasses the matrix stages and the existing transfer APIs retain it.
                        SimdUtils.Deinterleave4(block, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b, out Vector512<float> a);
                        SimdUtils.Transform3(in sourceMatrix, r, g, b, out r, out g, out b);
                        if (this.adapt)
                        {
                            SimdUtils.Transform3(in this.adaptationMatrix, r, g, b, out r, out g, out b);
                            r *= Vector512.Create(this.scale.X);
                            g *= Vector512.Create(this.scale.Y);
                            b *= Vector512.Create(this.scale.Z);
                            SimdUtils.Transform3(in this.inverseAdaptationMatrix, r, g, b, out r, out g, out b);
                        }

                        SimdUtils.Transform3(in targetMatrix, r, g, b, out r, out g, out b);
                        SimdUtils.Interleave4(r, g, b, a, ref Unsafe.Add(ref first, (nuint)i * 4));
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    nuint count = (block.Length - i).Vector256Count<float>();
                    for (nuint group = 0; group < count; group++, i += Vector256<float>.Count)
                    {
                        // Keep all connection-space stages in component registers. Each lane is one color;
                        // alpha bypasses the matrix stages and the existing transfer APIs retain it.
                        SimdUtils.Deinterleave4(block, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b, out Vector256<float> a);
                        SimdUtils.Transform3(in sourceMatrix, r, g, b, out r, out g, out b);
                        if (this.adapt)
                        {
                            SimdUtils.Transform3(in this.adaptationMatrix, r, g, b, out r, out g, out b);
                            r *= Vector256.Create(this.scale.X);
                            g *= Vector256.Create(this.scale.Y);
                            b *= Vector256.Create(this.scale.Z);
                            SimdUtils.Transform3(in this.inverseAdaptationMatrix, r, g, b, out r, out g, out b);
                        }

                        SimdUtils.Transform3(in targetMatrix, r, g, b, out r, out g, out b);
                        SimdUtils.Interleave4(r, g, b, a, ref Unsafe.Add(ref first, (nuint)i * 4));
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    nuint count = (block.Length - i).Vector128Count<float>();
                    for (nuint group = 0; group < count; group++, i += Vector128<float>.Count)
                    {
                        // Keep all connection-space stages in component registers. Each lane is one color;
                        // alpha bypasses the matrix stages and the existing transfer APIs retain it.
                        SimdUtils.Deinterleave4(block, i, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b, out Vector128<float> a);
                        SimdUtils.Transform3(in sourceMatrix, r, g, b, out r, out g, out b);
                        if (this.adapt)
                        {
                            SimdUtils.Transform3(in this.adaptationMatrix, r, g, b, out r, out g, out b);
                            r *= Vector128.Create(this.scale.X);
                            g *= Vector128.Create(this.scale.Y);
                            b *= Vector128.Create(this.scale.Z);
                            SimdUtils.Transform3(in this.inverseAdaptationMatrix, r, g, b, out r, out g, out b);
                        }

                        SimdUtils.Transform3(in targetMatrix, r, g, b, out r, out g, out b);
                        SimdUtils.Interleave4(r, g, b, a, ref Unsafe.Add(ref first, (nuint)i * 4));
                    }
                }

                // The tail uses the same operation order, including each separately rounded matrix stage.
                for (; i < block.Length; i++)
                {
                    Vector4 value = block[i];
                    Vector3 color = Vector3.Transform(value.AsVector3(), sourceMatrix);
                    if (this.adapt)
                    {
                        color = Vector3.Transform(color, this.adaptationMatrix) * this.scale;
                        color = Vector3.Transform(color, this.inverseAdaptationMatrix);
                    }

                    block[i] = new Vector4(Vector3.Transform(color, targetMatrix), value.W);
                }

                this.target.Compress(block);
            }
        }
    }
}
