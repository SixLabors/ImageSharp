// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Unpacks the stored samples of a bit-field bitmap row to pixels.
/// </summary>
/// <remarks>
/// <para>
/// A bit-field bitmap stores each pixel as one 16-bit or 32-bit little-endian sample. Four masks select the red,
/// green, blue and alpha bits of the sample. A channel with no mask gives 0. A bitmap with no alpha mask is opaque.
/// </para>
/// <para>
/// When each channel has 8 bits or fewer, <see cref="Unpack{TSample}"/> writes 4-byte pixels. The byte order is
/// R, G, B, A (<see cref="Rgba32"/>) or B, G, R, A (<see cref="Bgra32"/>). Each channel widens to the nearest 8-bit
/// value by the Direct3D UNORM rules. The shift, mask and widen values are the same for all pixels. Thus each SIMD
/// lane does the same scalar operations on one sample, and all register widths give the same result as the scalar form.
/// </para>
/// <para>
/// When a channel has more than 8 bits, a 4-byte pixel cannot hold its precision. Then
/// <see cref="UnpackScaled{TSample}"/> writes each channel as a value from 0 to 1.
/// </para>
/// </remarks>
internal readonly partial struct BmpBitFields
{
    private readonly Channel red;
    private readonly Channel green;
    private readonly Channel blue;
    private readonly Channel alpha;

    // Alpha 255 at bits 24..31 when the bitmap has no alpha mask. The alpha channel then gives 0, so OR sets 255.
    private readonly uint opaque;

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpBitFields"/> struct.
    /// </summary>
    /// <param name="redMask">The mask of the red bits.</param>
    /// <param name="greenMask">The mask of the green bits.</param>
    /// <param name="blueMask">The mask of the blue bits.</param>
    /// <param name="alphaMask">The mask of the alpha bits, or 0 for an opaque bitmap.</param>
    /// <param name="bgraOrder">
    /// <see langword="true"/> to write B, G, R, A pixels (<see cref="Bgra32"/>). <see langword="false"/> to write R, G,
    /// B, A pixels (<see cref="Rgba32"/>).
    /// </param>
    public BmpBitFields(uint redMask, uint greenMask, uint blueMask, uint alphaMask, bool bgraOrder)
    {
        // A 4-byte pixel as a little-endian uint has its first byte at bits 0..7 and its last byte at bits 24..31.
        // R, G, B, A order puts red first. B, G, R, A order puts blue first. Alpha is last in both orders.
        this.red = new Channel(redMask, bgraOrder ? 16 : 0);
        this.green = new Channel(greenMask, 8);
        this.blue = new Channel(blueMask, bgraOrder ? 0 : 16);
        this.alpha = new Channel(alphaMask, 24);
        this.opaque = alphaMask == 0 ? 0xFF000000U : 0U;
        this.IsByteRange = this.red.Bits <= 8 && this.green.Bits <= 8 && this.blue.Bits <= 8 && this.alpha.Bits <= 8;
    }

    /// <summary>
    /// Gets a value indicating whether each channel has 8 bits or fewer.
    /// </summary>
    /// <remarks>
    /// <see cref="Unpack{TSample}"/> requires this. A channel with more than 8 bits has more precision than a 4-byte
    /// pixel can hold, so the caller uses <see cref="UnpackScaled{TSample}"/> for it.
    /// </remarks>
    public bool IsByteRange { get; }

    /// <summary>
    /// Unpacks one row of stored samples to 4-byte pixels in the byte order of the constructor.
    /// </summary>
    /// <typeparam name="TSample">The stored sample width.</typeparam>
    /// <param name="source">The stored row. It holds at least one sample for each destination pixel.</param>
    /// <param name="destination">The destination pixels, one little-endian uint for each pixel.</param>
    public void Unpack<TSample>(ReadOnlySpan<byte> source, Span<uint> destination)
        where TSample : struct, ISampleOperator
    {
        DebugGuard.IsTrue(this.IsByteRange, "Each channel must have 8 bits or fewer.");
        DebugGuard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length * TSample.BytesPerSample, nameof(source));

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref uint destinationBase = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)destination.Length;
        nuint x = 0;

        // Each lane holds one pixel: a sample loads zero-extended to 32 bits, and the unpacked pixel stores as one
        // uint. A register of N lanes reads N samples and writes N pixels, so the loop bounds stay in pixels. The
        // narrower widths take the remainder of the wider ones. The scalar loop takes the last 0 to 3 pixels.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector512<uint>.Count <= length; x += (uint)Vector512<uint>.Count)
            {
                this.Unpack(TSample.Load512(ref sourceBase, x)).StoreUnsafe(ref destinationBase, x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector256<uint>.Count <= length; x += (uint)Vector256<uint>.Count)
            {
                this.Unpack(TSample.Load256(ref sourceBase, x)).StoreUnsafe(ref destinationBase, x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + (uint)Vector128<uint>.Count <= length; x += (uint)Vector128<uint>.Count)
            {
                this.Unpack(TSample.Load128(ref sourceBase, x)).StoreUnsafe(ref destinationBase, x);
            }
        }

        for (; x < length; x++)
        {
            Unsafe.Add(ref destinationBase, x) = this.Unpack(TSample.Load(ref sourceBase, x));
        }
    }

    /// <summary>
    /// Unpacks one row of stored samples to unassociated R, G, B, A values from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Each channel divides its value by the largest value of its mask. Thus a channel with more than 8 bits keeps
    /// its precision. This path is for masks that <see cref="IsByteRange"/> does not allow, so it is scalar.
    /// </remarks>
    /// <typeparam name="TSample">The stored sample width.</typeparam>
    /// <param name="source">The stored row. It holds at least one sample for each destination pixel.</param>
    /// <param name="destination">The destination values, one <see cref="Vector4"/> for each pixel.</param>
    public void UnpackScaled<TSample>(ReadOnlySpan<byte> source, Span<Vector4> destination)
        where TSample : struct, ISampleOperator
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length * TSample.BytesPerSample, nameof(source));

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);

        // With no alpha mask the alpha value is 0. Adding 1 to W then makes the pixel opaque.
        Vector4 opaqueAlpha = new(0, 0, 0, this.opaque == 0 ? 0F : 1F);

        for (int x = 0; x < destination.Length; x++)
        {
            uint sample = TSample.Load(ref sourceBase, (uint)x);
            destination[x] = new Vector4(
                this.red.Scale(sample),
                this.green.Scale(sample),
                this.blue.Scale(sample),
                this.alpha.Scale(sample)) + opaqueAlpha;
        }
    }

    /// <summary>
    /// Unpacks one sample to one 4-byte pixel as a little-endian uint.
    /// </summary>
    /// <param name="sample">The sample, zero-extended to 32 bits.</param>
    /// <returns>The pixel.</returns>
    /// <remarks>
    /// Each channel gives its 8-bit value in its own byte of the uint, with zeros in the other bytes. Thus OR joins
    /// the four channels into one pixel. When the bitmap has no alpha mask, <see cref="opaque"/> sets byte 3 to 255.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Unpack(uint sample)
        => this.red.Unpack(sample) | this.green.Unpack(sample) | this.blue.Unpack(sample) | this.alpha.Unpack(sample) | this.opaque;

    /// <inheritdoc cref="Unpack(uint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector128<uint> Unpack(Vector128<uint> sample)
        => this.red.Unpack(sample) | this.green.Unpack(sample) | this.blue.Unpack(sample) | this.alpha.Unpack(sample) | Vector128.Create(this.opaque);

    /// <inheritdoc cref="Unpack(uint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector256<uint> Unpack(Vector256<uint> sample)
        => this.red.Unpack(sample) | this.green.Unpack(sample) | this.blue.Unpack(sample) | this.alpha.Unpack(sample) | Vector256.Create(this.opaque);

    /// <inheritdoc cref="Unpack(uint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector512<uint> Unpack(Vector512<uint> sample)
        => this.red.Unpack(sample) | this.green.Unpack(sample) | this.blue.Unpack(sample) | this.alpha.Unpack(sample) | Vector512.Create(this.opaque);

    /// <summary>
    /// Extracts one channel from a sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The channel value is <c>v = (sample &gt;&gt; shift) &amp; low</c>. Its bit count <c>n</c> is the position of
    /// the highest bit of <c>low</c>. For a contiguous mask, <c>n</c> is the bit count of the mask. For a mask with
    /// gaps, <c>v</c> is still less than <c>2^n</c>, so a value never reaches the next channel.
    /// </para>
    /// <para>
    /// The Direct3D UNORM rules give the 8-bit value as <c>round(v * 255 / low)</c>. <c>low</c> is odd, because the shift
    /// removes the trailing zero bits of the mask. Thus <c>v * 255 / low</c> is never a half, and
    /// <c>(v * 255 + low / 2) / low</c> is the rounded value. A channel with no mask gives 0.
    /// </para>
    /// </remarks>
    private readonly struct Channel
    {
        // The lanes divide by low with one multiply and one shift: (x * ceil(2^24 / low)) >> 24. A vector integer
        // division does more work. On x86, the JIT checks the divisor for zero, converts the lanes to double, divides
        // and converts back. The result equals x / low when x * low is below 2^24. The largest x * low is
        // (255 * 255 + 127) * 255, and the largest product fits 32 bits.

        /// <summary>
        /// The shift of the division by <c>low</c>.
        /// </summary>
        private const int DivideShift = 24;

        private readonly int shift;
        private readonly uint low;
        private readonly uint bias;
        private readonly uint multiplier;
        private readonly int position;
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> struct.
        /// </summary>
        /// <param name="mask">The channel mask.</param>
        /// <param name="position">The bit position of the channel in a 4-byte pixel uint.</param>
        public Channel(uint mask, int position)
        {
            this.position = position;
            if (mask == 0)
            {
                // The zero multiplier and the zero scale make the channel 0. The zero shift keeps the vector shift
                // count in range.
                return;
            }

            // The trailing zero count is the bit position of the lowest mask bit. A right shift by it moves the
            // channel to bit 0. The shifted mask then selects the channel bits only.
            // Example: the 5-6-5 green mask 0x07E0 (0b0000_0111_1110_0000) gives shift 5 and low 0x3F (0b11_1111).
            this.shift = BitOperations.TrailingZeroCount(mask);
            this.low = mask >> this.shift;

            // The channel width is the position of the highest bit of low, plus 1. For 0x3F that is 6.
            this.Bits = 32 - BitOperations.LeadingZeroCount(this.low);

            // The largest channel value is low, so the scaled value is from 0 to 1.
            this.scale = 1F / this.low;

            if (this.Bits <= 8)
            {
                // The bias rounds to the nearest value. The multiplier divides by low, as the comment above DivideShift explains.
                // Example: 5 bits. low = 31, bias = 15, multiplier = 541201. For v = 3, x = 3 * 255 + 15 = 780 and
                // (780 * 541201) >> 24 = 25, the nearest 8-bit value to 3 * 255 / 31 = 24.68.
                this.bias = this.low / 2;
                this.multiplier = (uint)(((1UL << DivideShift) + this.low - 1) / this.low);
            }
        }

        /// <summary>
        /// Gets the bit count of the channel value, or 0 for a channel with no mask.
        /// </summary>
        public int Bits { get; }

        /// <summary>
        /// Gets the channel value of one sample, scaled to the range from 0 to 1.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns>The scaled value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Scale(uint sample) => ((sample >> this.shift) & this.low) * this.scale;

        /// <summary>
        /// Unpacks the channel of one sample.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns>The 8-bit channel value at its position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Unpack(uint sample)
        {
            // 1. Move the channel to bit 0 and remove the other channels: v = (sample >> shift) & low.
            // 2. Widen v to the nearest 8-bit value: (((v * 255) + bias) * multiplier) >> 24. The result is 0 to 255.
            // 3. Move the 8-bit value to its byte in the pixel uint: << position. The bytes of the four channels do
            //    not overlap, so the caller ORs them together.
            uint value = (sample >> this.shift) & this.low;
            return ((((value * 255) + this.bias) * this.multiplier) >> DivideShift) << this.position;
        }

        /// <inheritdoc cref="Unpack(uint)"/>
        /// <remarks>
        /// Each lane does the three steps of <see cref="Unpack(uint)"/> on its own sample. The shift counts are the
        /// same for all lanes, so each shift is one instruction with a scalar count.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<uint> Unpack(Vector128<uint> sample)
        {
            Vector128<uint> value = Vector128.ShiftRightLogical(sample, this.shift) & Vector128.Create(this.low);
            value = ((value * 255) + Vector128.Create(this.bias)) * this.multiplier;
            return Vector128.ShiftLeft(Vector128.ShiftRightLogical(value, DivideShift), this.position);
        }

        /// <inheritdoc cref="Unpack(Vector128{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<uint> Unpack(Vector256<uint> sample)
        {
            Vector256<uint> value = Vector256.ShiftRightLogical(sample, this.shift) & Vector256.Create(this.low);
            value = ((value * 255) + Vector256.Create(this.bias)) * this.multiplier;
            return Vector256.ShiftLeft(Vector256.ShiftRightLogical(value, DivideShift), this.position);
        }

        /// <inheritdoc cref="Unpack(Vector128{uint})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<uint> Unpack(Vector512<uint> sample)
        {
            Vector512<uint> value = Vector512.ShiftRightLogical(sample, this.shift) & Vector512.Create(this.low);
            value = ((value * 255) + Vector512.Create(this.bias)) * this.multiplier;
            return Vector512.ShiftLeft(Vector512.ShiftRightLogical(value, DivideShift), this.position);
        }
    }
}
