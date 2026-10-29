// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Owns SIMD dispatch and vector-span traversal for a configured ICC operator.
/// Typed model conversion is handled by the caller, so each operator has one bulk transform loop.
/// </summary>
/// <typeparam name="TOperator">The operator containing the selected profile stages and their constants.</typeparam>
internal sealed class IccColorConverter<TOperator> : IIccColorConverter
    where TOperator : struct, IIccColorConverterOperator
{
    // A readonly generic receiver causes a defensive copy before each constrained interface call.
    // Keep this private field writable so conversion uses its address; the profile operators do not mutate their state.
    private TOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccColorConverter{TOperator}"/> class.
    /// </summary>
    /// <param name="colorOperator">The complete transform, configured before pixel processing begins.</param>
    public IccColorConverter(TOperator colorOperator) => this.colorOperator = colorOperator;

    /// <summary>
    /// Gets the configured operator for construction of a profile-to-profile connection.
    /// </summary>
    public TOperator Operator => this.colorOperator;

    /// <inheritdoc/>
    public bool IsTrc => this.colorOperator.IsTrc;

    /// <inheritdoc/>
    public bool Is16BitLut => this.colorOperator.Is16BitLut;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Convert(Vector4 value) => this.colorOperator.Convert(value);

    /// <inheritdoc/>
    public void Convert(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        ref float target = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(destination));
        int i = 0;

        // Each operator sees one component per register and one independent color per lane.
        // Read a whole block before writing it, so the source and destination can share storage.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave4(source, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w);
                this.colorOperator.Convert(ref x, ref y, ref z, ref w);
                SimdUtils.Interleave4(x, y, z, w, ref Unsafe.Add(ref target, (uint)i * 4));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave4(source, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w);
                this.colorOperator.Convert(ref x, ref y, ref z, ref w);
                SimdUtils.Interleave4(x, y, z, w, ref Unsafe.Add(ref target, (uint)i * 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                ref Vector128<float> first = ref Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.AsRef(in source[i]));
                SimdUtils.Transpose4(
                    first,
                    Unsafe.Add(ref first, 1U),
                    Unsafe.Add(ref first, 2U),
                    Unsafe.Add(ref first, 3U),
                    out Vector128<float> x,
                    out Vector128<float> y,
                    out Vector128<float> z,
                    out Vector128<float> w);

                this.colorOperator.Convert(ref x, ref y, ref z, ref w);

                // Transpose back to four [X Y Z W] colors. Stores cover exactly four destination colors.
                SimdUtils.Transpose4(
                    x,
                    y,
                    z,
                    w,
                    out Vector128<float> pixel0,
                    out Vector128<float> pixel1,
                    out Vector128<float> pixel2,
                    out Vector128<float> pixel3);

                nuint offset = (uint)i * 4;
                pixel0.StoreUnsafe(ref target, offset);
                pixel1.StoreUnsafe(ref target, offset + 4);
                pixel2.StoreUnsafe(ref target, offset + 8);
                pixel3.StoreUnsafe(ref target, offset + 12);
            }
        }

        // This also processes the complete input when SIMD is unavailable.
        for (; i < source.Length; i++)
        {
            destination[i] = this.colorOperator.Convert(source[i]);
        }
    }
}
