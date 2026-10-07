// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

internal abstract partial class WebpColorConverterBase
{
    private static readonly Vector128<byte>[] PlanarMasks128 = CreatePlanarMasks128();
    private static readonly Vector256<byte>[] PlanarMasks256 = CreatePlanarMasks256();
    private static readonly Vector512<byte>[] PlanarMasks512 = CreatePlanarMasks512();
    private static readonly Vector512<int>[] PlanarLaneIndices512 = CreatePlanarLaneIndices();

    // Only W has all mask bits set. Wider masks repeat this RGBA layout for each pixel.
    private static readonly Vector128<float> AlphaMaskFloat128 = Vector128.Create(0, 0, 0, -1).AsSingle();
    private static readonly Vector256<float> AlphaMaskFloat256 = Vector256.Create(AlphaMaskFloat128, AlphaMaskFloat128);
    private static readonly Vector512<float> AlphaMaskFloat512 = Vector512.Create(AlphaMaskFloat256, AlphaMaskFloat256);

    /// <summary>
    /// Packs straight scaled vectors produced by an ICC conversion.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The decoding configuration.</param>
    /// <param name="row">The converted straight RGB values.</param>
    /// <param name="alpha">The separate alpha row, or empty to preserve vector alpha.</param>
    /// <param name="destination">The destination pixels.</param>
    protected static void PackIccRgbRow<TPixel>(Configuration configuration, Span<Vector4> row, ReadOnlySpan<byte> alpha, Span<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!alpha.IsEmpty)
        {
            // ICC transforms straight RGB. Add the separate alpha afterwards; each byte maps to W as alpha / 255.
            ref float rowBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(row));

            ref byte alphaBase = ref MemoryMarshal.GetReference(alpha);
            int pixelOffset = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                // Four RGBA pixels occupy sixteen float lanes. Repeat each alpha across its pixel, then select only W.
                Vector512<int> alphaIndices = Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3);

                int pixelEnd = (int)Numerics.Vector512Count<float>(row.Length * 4) * 4;

                // Sixteen float lanes hold four RGBA pixels, so advance four pixels per vector.
                // pixelEnd excludes any partial group that cannot fill the vector.
                for (; pixelOffset < pixelEnd; pixelOffset += 4)
                {
                    // Read exactly four bytes and widen them to four floats; the final block cannot read past alpha.
                    Vector128<byte> bytes = Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref alphaBase, (uint)pixelOffset))).AsByte();

                    Vector128<uint> alphaCodes = Vector128.WidenLower(Vector128.WidenLower(bytes));
                    Vector128<float> normalizedAlpha = Vector128.ConvertToSingle(alphaCodes.AsInt32()) / Vector128.Create((float)byte.MaxValue);
                    Vector512<float> alphaValues = Vector512.Shuffle(normalizedAlpha.ToVector256().ToVector512(), alphaIndices);
                    nuint floatOffset = (nuint)pixelOffset * 4;
                    Vector512<float> pixels = Vector512.LoadUnsafe(ref rowBase, floatOffset);

                    // The mask replaces W bits only, preserving every ICC-produced RGB value exactly.
                    Vector512.ConditionalSelect(AlphaMaskFloat512, alphaValues, pixels).StoreUnsafe(ref rowBase, floatOffset);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // Two RGBA pixels occupy eight float lanes. The same shuffle and W mask handle this width.
                Vector256<int> alphaIndices = Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1);

                int pixelEnd = pixelOffset + ((int)Numerics.Vector256Count<float>((row.Length - pixelOffset) * 4) * 2);

                // Eight float lanes hold two RGBA pixels, so advance two pixels per vector.
                // pixelEnd excludes any partial group that cannot fill the vector.
                for (; pixelOffset < pixelEnd; pixelOffset += 2)
                {
                    // Only two alpha bytes are required for these two pixels.
                    Vector128<byte> bytes = Vector128.CreateScalar(
                        (uint)Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref alphaBase, (uint)pixelOffset))).AsByte();

                    Vector128<uint> alphaCodes = Vector128.WidenLower(Vector128.WidenLower(bytes));
                    Vector128<float> normalizedAlpha = Vector128.ConvertToSingle(alphaCodes.AsInt32()) / Vector128.Create((float)byte.MaxValue);
                    Vector256<float> alphaValues = Vector256.Shuffle(normalizedAlpha.ToVector256(), alphaIndices);
                    nuint floatOffset = (nuint)pixelOffset * 4;
                    Vector256<float> pixels = Vector256.LoadUnsafe(ref rowBase, floatOffset);
                    Vector256.ConditionalSelect(AlphaMaskFloat256, alphaValues, pixels).StoreUnsafe(ref rowBase, floatOffset);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // One RGBA pixel fills a 128-bit vector. The W mask keeps its RGB bits unchanged.
                ReadOnlySpan<float> remaining = MemoryMarshal.Cast<Vector4, float>(row[pixelOffset..]);
                int pixelEnd = pixelOffset + (int)remaining.Vector128Count<float>();

                for (; pixelOffset < pixelEnd; pixelOffset++)
                {
                    Vector128<float> alphaValues = Vector128.Create((float)alpha[pixelOffset]) / Vector128.Create((float)byte.MaxValue);
                    nuint floatOffset = (nuint)pixelOffset * 4;
                    Vector128<float> pixels = Vector128.LoadUnsafe(ref rowBase, floatOffset);
                    Vector128.ConditionalSelect(AlphaMaskFloat128, alphaValues, pixels).StoreUnsafe(ref rowBase, floatOffset);
                }
            }

            // Complete the remaining pixels, or the whole row when SIMD is unavailable.
            for (; pixelOffset < row.Length; pixelOffset++)
            {
                row[pixelOffset].W = alpha[pixelOffset] / (float)byte.MaxValue;
            }
        }

        // These vectors are straight RGBA. The explicit source contract lets TPixel apply its required alpha representation.
        PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, row, destination);
    }

    /// <summary>
    /// Creates cached masks that interleave three 16-byte planes into three packed output vectors.
    /// </summary>
    /// <returns>Three masks per packed output block, ordered by source channel.</returns>
    private static Vector128<byte>[] CreatePlanarMasks128()
    {
        Vector128<byte>[] masks = new Vector128<byte>[9];
        InlineArray16<byte> mask = default;

        // Each of the three output vectors needs one mask per source channel.
        for (int block = 0; block < 3; block++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                FillPlanarMask(mask, block * Vector128<byte>.Count, channel);
                masks[(block * 3) + channel] = Vector128.LoadUnsafe(ref mask[0]);
            }
        }

        return masks;
    }

    /// <summary>
    /// Creates cached masks that interleave three 32-byte planes into three packed output vectors.
    /// </summary>
    /// <returns>Three masks per packed output block, ordered by source channel.</returns>
    private static Vector256<byte>[] CreatePlanarMasks256()
    {
        Vector256<byte>[] masks = new Vector256<byte>[9];
        InlineArray32<byte> mask = default;

        // Each of the three output vectors needs one mask per source channel.
        for (int block = 0; block < 3; block++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                FillPlanarMask(mask, block * Vector256<byte>.Count, channel);
                masks[(block * 3) + channel] = Vector256.LoadUnsafe(ref mask[0]);
            }
        }

        return masks;
    }

    /// <summary>
    /// Creates cached masks that interleave three 64-byte planes into three packed output vectors.
    /// </summary>
    /// <returns>Three masks per packed output block, ordered by source channel.</returns>
    private static Vector512<byte>[] CreatePlanarMasks512()
    {
        Vector512<byte>[] masks = new Vector512<byte>[9];
        InlineArray64<byte> mask = default;

        // Each of the three output vectors needs one mask per source channel.
        for (int block = 0; block < 3; block++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                FillPlanarMask(mask, block * Vector512<byte>.Count, channel);
                masks[(block * 3) + channel] = Vector512.LoadUnsafe(ref mask[0]);
            }
        }

        return masks;
    }

    /// <summary>
    /// Selects the source's 128-bit lanes needed by each packed 512-bit output block.
    /// </summary>
    /// <returns>The source lane indices for each of the three packed output blocks.</returns>
    private static Vector512<int>[] CreatePlanarLaneIndices()
    {
        Vector512<int>[] indices = new Vector512<int>[3];
        InlineArray16<int> mask = default;

        for (int block = 0; block < 3; block++)
        {
            // Each output lane holds 16 packed bytes. Its samples lie in one 16-byte source lane because
            // a source lane's 16 pixels occupy exactly three output lanes: 16 pixels * 3 bytes = 48 bytes.
            for (int i = 0; i < Vector512<int>.Count; i++)
            {
                int outputByte = (block * Vector512<byte>.Count) + ((i / 4) * Vector128<byte>.Count);
                int sourceLane = (outputByte / 3) / Vector128<byte>.Count;

                // Four int indices copy that source lane without changing its byte order.
                mask[i] = (sourceLane * 4) + (i % 4);
            }

            indices[block] = Vector512.LoadUnsafe(ref mask[0]);
        }

        return indices;
    }

    /// <summary>
    /// Selects one channel's plane indices for a packed output block and zeros the other channel positions.
    /// </summary>
    /// <param name="mask">The shuffle indices for one output vector.</param>
    /// <param name="outputOffset">The block's byte offset in the packed output.</param>
    /// <param name="channel">The channel's position within each three-byte pixel.</param>
    private static void FillPlanarMask(Span<byte> mask, int outputOffset, int channel)
    {
        // Packed bytes repeat channels 0, 1, 2. Dividing their offset by three selects the pixel in the full source plane.
        // Index 255 is outside every supported vector width. The shuffle returns zero at these other channel positions.
        for (int i = 0; i < mask.Length; i++)
        {
            int outputByte = outputOffset + i;
            mask[i] = outputByte % 3 == channel ? (byte)(outputByte / 3) : byte.MaxValue;
        }
    }

    private sealed partial class WebpColorConverter<TOperator>
    {
        /// <inheritdoc />
        public override void UpSample(
            Span<byte> topY,
            Span<byte> bottomY,
            Span<byte> topU,
            Span<byte> topV,
            Span<byte> curU,
            Span<byte> curV,
            Span<byte> topDestination,
            Span<byte> bottomDestination,
            int length,
            Span<byte> uvBuffer)
        {
            // Chroma interpolation and color conversion share the same block width. The scalar path uses identical rounding.
            if (!Vector128.IsHardwareAccelerated)
            {
                UpSampleScalar(topY, bottomY, topU, topV, curU, curV, topDestination, bottomDestination, length);
                return;
            }

            UpSampleVector(topY, bottomY, topU, topV, curU, curV, topDestination, bottomDestination, length, uvBuffer);
        }

        /// <summary>
        /// Converts one interpolated pixel using the selected native color operation.
        /// </summary>
        /// <param name="y">The luma or first source channel.</param>
        /// <param name="u">The first chroma or second source channel.</param>
        /// <param name="v">The second chroma or third source channel.</param>
        /// <param name="destination">Three destination byte codes.</param>
        private static void ConvertPixel(int y, int u, int v, Span<byte> destination)
        {
            TOperator.ConvertToSamples(ref y, ref u, ref v);
            destination[0] = (byte)y;
            destination[1] = (byte)u;
            destination[2] = (byte)v;
        }

        /// <summary>
        /// Converts sixty-four interpolated samples and interleaves their three output channels.
        /// </summary>
        /// <param name="y">Sixty-four luma or first-channel byte codes.</param>
        /// <param name="u">Sixty-four first-chroma or second-channel byte codes.</param>
        /// <param name="v">Sixty-four second-chroma or third-channel byte codes.</param>
        /// <param name="destination">The 192 packed output bytes.</param>
        private static void Convert64(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, Span<byte> destination)
        {
            ref byte yRef = ref MemoryMarshal.GetReference(y);
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);

            // Four sixteen-pixel groups produce each 64-byte output channel plane in source order.
            Convert16Vector512(
                ref yRef, ref uRef, ref vRef, out Vector128<byte> channel0Group0, out Vector128<byte> channel1Group0, out Vector128<byte> channel2Group0);

            ref byte y16 = ref Unsafe.Add(ref yRef, 16U);

            ref byte u16 = ref Unsafe.Add(ref uRef, 16U);
            ref byte v16 = ref Unsafe.Add(ref vRef, 16U);
            Convert16Vector512(
                ref y16, ref u16, ref v16, out Vector128<byte> channel0Group1, out Vector128<byte> channel1Group1, out Vector128<byte> channel2Group1);

            ref byte y32 = ref Unsafe.Add(ref yRef, 32U);

            ref byte u32 = ref Unsafe.Add(ref uRef, 32U);
            ref byte v32 = ref Unsafe.Add(ref vRef, 32U);
            Convert16Vector512(
                ref y32, ref u32, ref v32, out Vector128<byte> channel0Group2, out Vector128<byte> channel1Group2, out Vector128<byte> channel2Group2);

            ref byte y48 = ref Unsafe.Add(ref yRef, 48U);

            ref byte u48 = ref Unsafe.Add(ref uRef, 48U);
            ref byte v48 = ref Unsafe.Add(ref vRef, 48U);
            Convert16Vector512(
                ref y48, ref u48, ref v48, out Vector128<byte> channel0Group3, out Vector128<byte> channel1Group3, out Vector128<byte> channel2Group3);

            PlanarTo24b(
                Vector512.Create(Vector256.Create(channel0Group0, channel0Group1), Vector256.Create(channel0Group2, channel0Group3)),
                Vector512.Create(Vector256.Create(channel1Group0, channel1Group1), Vector256.Create(channel1Group2, channel1Group3)),
                Vector512.Create(Vector256.Create(channel2Group0, channel2Group1), Vector256.Create(channel2Group2, channel2Group3)),
                destination);
        }

        /// <summary>
        /// Converts thirty-two byte samples with 256-bit integer lanes and packs the channel planes.
        /// </summary>
        /// <param name="y">The luma byte codes in pixel order.</param>
        /// <param name="u">The U chroma byte codes in pixel order.</param>
        /// <param name="v">The V chroma byte codes in pixel order.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void Convert32(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, Span<byte> destination)
        {
            ref byte yRef = ref MemoryMarshal.GetReference(y);
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);

            // Each helper converts sixteen consecutive pixels, then the packed store interleaves complete channel planes.
            Convert16Vector256(
                ref yRef, ref uRef, ref vRef, out Vector128<byte> channel0Group0, out Vector128<byte> channel1Group0, out Vector128<byte> channel2Group0);

            ref byte y16 = ref Unsafe.Add(ref yRef, 16U);

            ref byte u16 = ref Unsafe.Add(ref uRef, 16U);
            ref byte v16 = ref Unsafe.Add(ref vRef, 16U);
            Convert16Vector256(
                ref y16, ref u16, ref v16, out Vector128<byte> channel0Group1, out Vector128<byte> channel1Group1, out Vector128<byte> channel2Group1);

            PlanarTo24b(
                Vector256.Create(channel0Group0, channel0Group1),
                Vector256.Create(channel1Group0, channel1Group1),
                Vector256.Create(channel2Group0, channel2Group1),
                destination);
        }

        /// <summary>
        /// Converts sixteen byte samples with four 128-bit integer groups and packs them as a row block.
        /// </summary>
        /// <param name="y">The luma byte codes in pixel order.</param>
        /// <param name="u">The U chroma byte codes in pixel order.</param>
        /// <param name="v">The V chroma byte codes in pixel order.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void Convert16(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, Span<byte> destination)
        {
            ref byte yRef = ref MemoryMarshal.GetReference(y);
            ref byte uRef = ref MemoryMarshal.GetReference(u);
            ref byte vRef = ref MemoryMarshal.GetReference(v);

            // Each helper converts sixteen consecutive pixels, then the packed store interleaves complete channel planes.
            Convert16Vector128(ref yRef, ref uRef, ref vRef, out Vector128<byte> c0, out Vector128<byte> c1, out Vector128<byte> c2);
            PlanarTo24b(c0, c1, c2, destination);
        }

        /// <summary>
        /// Loads exactly sixteen byte codes into sixteen integer lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector512<int> Load16(ref byte source)
        {
            Vector128<byte> bytes = Unsafe.ReadUnaligned<Vector128<byte>>(ref source);
            (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(bytes);

            // Byte codes are at most 255, so their widened words remain positive when interpreted as signed for this helper.
            return Vector512.Create(Vector256_.Widen(lower.AsInt16()), Vector256_.Widen(upper.AsInt16()));
        }

        /// <summary>
        /// Loads exactly eight byte codes into eight integer lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector256<int> Load8(ref byte source)
        {
            Vector128<byte> bytes = Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte();
            (Vector128<ushort> lower, _) = Vector128.Widen(bytes);

            // The widened byte codes are 0..255, so the signed word conversion preserves their values.
            return Vector256_.Widen(lower.AsInt16());
        }

        /// <summary>
        /// Loads exactly four byte codes into four integer lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector128<int> Load4(ref byte source)
        {
            Vector128<byte> bytes = Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            (Vector128<ushort> lower, _) = Vector128.Widen(bytes);
            (Vector128<uint> result, _) = Vector128.Widen(lower);
            return result.AsInt32();
        }

        /// <summary>
        /// Applies the selected color operation to sixteen samples using one 512-bit group.
        /// </summary>
        /// <param name="y">The luma byte codes in pixel order.</param>
        /// <param name="u">The U chroma byte codes in pixel order.</param>
        /// <param name="v">The V chroma byte codes in pixel order.</param>
        /// <param name="c0">The first output channel plane in pixel order.</param>
        /// <param name="c1">The second output channel plane in pixel order.</param>
        /// <param name="c2">The third output channel plane in pixel order.</param>
        private static void Convert16Vector512(ref byte y, ref byte u, ref byte v, out Vector128<byte> c0, out Vector128<byte> c1, out Vector128<byte> c2)
        {
            // Load consecutive sample groups in pixel order. Each signed int lane carries one unsigned byte code.
            Vector512<int> channel0Group0 = Load16(ref y);

            Vector512<int> channel1Group0 = Load16(ref u);
            Vector512<int> channel2Group0 = Load16(ref v);

            TOperator.ConvertToSamples(ref channel0Group0, ref channel1Group0, ref channel2Group0);

            // The operator returns byte-range channels. Narrow in the original group order before interleaving them.
            c0 = Narrow(channel0Group0);
            c1 = Narrow(channel1Group0);
            c2 = Narrow(channel2Group0);
        }

        /// <summary>
        /// Applies the selected color operation to sixteen samples using two 256-bit groups.
        /// </summary>
        /// <param name="y">The luma byte codes in pixel order.</param>
        /// <param name="u">The U chroma byte codes in pixel order.</param>
        /// <param name="v">The V chroma byte codes in pixel order.</param>
        /// <param name="c0">The first output channel plane in pixel order.</param>
        /// <param name="c1">The second output channel plane in pixel order.</param>
        /// <param name="c2">The third output channel plane in pixel order.</param>
        private static void Convert16Vector256(ref byte y, ref byte u, ref byte v, out Vector128<byte> c0, out Vector128<byte> c1, out Vector128<byte> c2)
        {
            // Load consecutive sample groups in pixel order. Each signed int lane carries one unsigned byte code.
            Vector256<int> channel0Group0 = Load8(ref y);

            Vector256<int> channel1Group0 = Load8(ref u);
            Vector256<int> channel2Group0 = Load8(ref v);
            Vector256<int> channel0Group1 = Load8(ref Unsafe.Add(ref y, 8U));
            Vector256<int> channel1Group1 = Load8(ref Unsafe.Add(ref u, 8U));
            Vector256<int> channel2Group1 = Load8(ref Unsafe.Add(ref v, 8U));

            TOperator.ConvertToSamples(ref channel0Group0, ref channel1Group0, ref channel2Group0);
            TOperator.ConvertToSamples(ref channel0Group1, ref channel1Group1, ref channel2Group1);

            // The operator returns byte-range channels. Narrow in the original group order before interleaving them.
            c0 = Narrow(channel0Group0, channel0Group1);
            c1 = Narrow(channel1Group0, channel1Group1);
            c2 = Narrow(channel2Group0, channel2Group1);
        }

        /// <summary>
        /// Applies the selected color operation to sixteen samples using four 128-bit groups.
        /// </summary>
        /// <param name="y">The luma byte codes in pixel order.</param>
        /// <param name="u">The U chroma byte codes in pixel order.</param>
        /// <param name="v">The V chroma byte codes in pixel order.</param>
        /// <param name="c0">The first output channel plane in pixel order.</param>
        /// <param name="c1">The second output channel plane in pixel order.</param>
        /// <param name="c2">The third output channel plane in pixel order.</param>
        private static void Convert16Vector128(ref byte y, ref byte u, ref byte v, out Vector128<byte> c0, out Vector128<byte> c1, out Vector128<byte> c2)
        {
            // Load consecutive sample groups in pixel order. Each signed int lane carries one unsigned byte code.
            Vector128<int> channel0Group0 = Load4(ref y);

            Vector128<int> channel1Group0 = Load4(ref u);
            Vector128<int> channel2Group0 = Load4(ref v);
            Vector128<int> channel0Group1 = Load4(ref Unsafe.Add(ref y, 4U));
            Vector128<int> channel1Group1 = Load4(ref Unsafe.Add(ref u, 4U));
            Vector128<int> channel2Group1 = Load4(ref Unsafe.Add(ref v, 4U));
            Vector128<int> channel0Group2 = Load4(ref Unsafe.Add(ref y, 8U));
            Vector128<int> channel1Group2 = Load4(ref Unsafe.Add(ref u, 8U));
            Vector128<int> channel2Group2 = Load4(ref Unsafe.Add(ref v, 8U));
            Vector128<int> channel0Group3 = Load4(ref Unsafe.Add(ref y, 12U));
            Vector128<int> channel1Group3 = Load4(ref Unsafe.Add(ref u, 12U));
            Vector128<int> channel2Group3 = Load4(ref Unsafe.Add(ref v, 12U));

            TOperator.ConvertToSamples(ref channel0Group0, ref channel1Group0, ref channel2Group0);
            TOperator.ConvertToSamples(ref channel0Group1, ref channel1Group1, ref channel2Group1);
            TOperator.ConvertToSamples(ref channel0Group2, ref channel1Group2, ref channel2Group2);
            TOperator.ConvertToSamples(ref channel0Group3, ref channel1Group3, ref channel2Group3);

            // The operator returns byte-range channels. Narrow in the original group order before interleaving them.
            c0 = Narrow(channel0Group0, channel0Group1, channel0Group2, channel0Group3);
            c1 = Narrow(channel1Group0, channel1Group1, channel1Group2, channel1Group3);
            c2 = Narrow(channel2Group0, channel2Group1, channel2Group2, channel2Group3);
        }

        /// <summary>
        /// Narrows sixteen clipped integer codes without changing their values.
        /// </summary>
        /// <param name="value">Sixteen consecutive channel codes, each in the range 0..255.</param>
        /// <returns>The sixteen channel codes as bytes, in their original order.</returns>
        private static Vector128<byte> Narrow(Vector512<int> value)
        {
            // The color operator already bounds each code to 0..255. Narrowing removes only zero upper bits and preserves group order.
            Vector256<short> halves = Vector256.Narrow(value.GetLower(), value.GetUpper());

            return Vector128.Narrow(halves.GetLower().AsUInt16(), halves.GetUpper().AsUInt16());
        }

        /// <summary>
        /// Narrows two groups of eight clipped integer codes.
        /// </summary>
        /// <param name="lower">The first eight channel codes, each in the range 0..255.</param>
        /// <param name="upper">The following eight channel codes, each in the range 0..255.</param>
        /// <returns>The sixteen channel codes as bytes, in their original order.</returns>
        private static Vector128<byte> Narrow(Vector256<int> lower, Vector256<int> upper)
        {
            // The color operator already bounds each code to 0..255. Narrowing removes only zero upper bits and preserves group order.
            Vector128<short> first = Vector128.Narrow(lower.GetLower(), lower.GetUpper());

            Vector128<short> second = Vector128.Narrow(upper.GetLower(), upper.GetUpper());
            return Vector128.Narrow(first.AsUInt16(), second.AsUInt16());
        }

        /// <summary>
        /// Narrows four groups of four clipped integer codes.
        /// </summary>
        /// <param name="group0">The first four channel codes, each in the range 0..255.</param>
        /// <param name="group1">The second four channel codes, each in the range 0..255.</param>
        /// <param name="group2">The third four channel codes, each in the range 0..255.</param>
        /// <param name="group3">The fourth four channel codes, each in the range 0..255.</param>
        /// <returns>The sixteen channel codes as bytes, in their original order.</returns>
        private static Vector128<byte> Narrow(Vector128<int> group0, Vector128<int> group1, Vector128<int> group2, Vector128<int> group3)
        {
            // The color operator already bounds each code to 0..255. Narrowing removes only zero upper bits and preserves group order.
            Vector128<short> first = Vector128.Narrow(group0, group1);

            Vector128<short> second = Vector128.Narrow(group2, group3);
            return Vector128.Narrow(first.AsUInt16(), second.AsUInt16());
        }

        /// <summary>
        /// Packs two byte chroma codes into adjacent low and high words for the scalar filter.
        /// </summary>
        /// <param name="u">The U byte code.</param>
        /// <param name="v">The V byte code.</param>
        /// <returns>U in bits 0..7 and V in bits 16..23, with room for the filter sums.</returns>
        private static uint LoadUv(byte u, byte v) => (uint)(u | (v << 16));

        /// <summary>
        /// Interpolates chroma and converts pixels with scalar arithmetic.
        /// </summary>
        /// <param name="topY">The first output row's luma samples.</param>
        /// <param name="bottomY">The second output row's luma samples, or empty for a single row.</param>
        /// <param name="topU">The preceding U chroma row.</param>
        /// <param name="topV">The preceding V chroma row.</param>
        /// <param name="curU">The current U chroma row.</param>
        /// <param name="curV">The current V chroma row.</param>
        /// <param name="topDestination">The first packed output row.</param>
        /// <param name="bottomDestination">The second packed output row, or empty for a single row.</param>
        /// <param name="length">The number of output pixels in each row.</param>
        private static void UpSampleScalar(
            ReadOnlySpan<byte> topY,
            ReadOnlySpan<byte> bottomY,
            ReadOnlySpan<byte> topU,
            ReadOnlySpan<byte> topV,
            ReadOnlySpan<byte> curU,
            ReadOnlySpan<byte> curV,
            Span<byte> topDestination,
            Span<byte> bottomDestination,
            int length)
        {
            const int bytesPerPixel = 3;
            int lastChromaPair = (length - 1) >> 1;

            // There is no left neighbor for pixel zero. Replicate its chroma horizontally and blend the two rows 3:1.
            // 0x00020002 adds two to both packed channels before division by four, rounding each channel separately.
            uint topLeft = LoadUv(topU[0], topV[0]);
            uint bottomLeft = LoadUv(curU[0], curV[0]);
            uint firstChroma = ((3 * topLeft) + bottomLeft + 0x00020002u) >> 2;
            ConvertPixel(topY[0], (int)(firstChroma & 0xff), (int)(firstChroma >> 16), topDestination);

            if (!bottomY.IsEmpty)
            {
                firstChroma = ((3 * bottomLeft) + topLeft + 0x00020002u) >> 2;
                ConvertPixel(bottomY[0], (int)firstChroma & 0xff, (int)(firstChroma >> 16), bottomDestination);
            }

            for (int x = 1; x <= lastChromaPair; x++)
            {
                uint topRight = LoadUv(topU[x], topV[x]);
                uint bottomRight = LoadUv(curU[x], curV[x]);

                // U and V occupy separate 16-bit words. The weighted sums remain below 65536, so U cannot carry into V.
                // Adding eight to each word rounds the 9:3:3:1 filter before division by sixteen, split across these shifts.
                uint roundedSum = topLeft + topRight + bottomLeft + bottomRight + 0x00080008u;
                uint crossDiagonal = (roundedSum + (2 * (topRight + bottomLeft))) >> 3;
                uint mainDiagonal = (roundedSum + (2 * (topLeft + bottomRight))) >> 3;
                firstChroma = (crossDiagonal + topLeft) >> 1;
                uint secondChroma = (mainDiagonal + topRight) >> 1;

                // Each pair of neighboring chroma samples supplies the odd pixel on its left and the following even pixel.
                int evenPixel = x * 2;

                ConvertPixel(topY[evenPixel - 1], (int)(firstChroma & 0xff), (int)(firstChroma >> 16), topDestination[((evenPixel - 1) * bytesPerPixel)..]);
                ConvertPixel(topY[evenPixel], (int)(secondChroma & 0xff), (int)(secondChroma >> 16), topDestination[(evenPixel * bytesPerPixel)..]);

                if (!bottomY.IsEmpty)
                {
                    firstChroma = (mainDiagonal + bottomLeft) >> 1;
                    secondChroma = (crossDiagonal + bottomRight) >> 1;
                    ConvertPixel(
                        bottomY[evenPixel - 1], (int)(firstChroma & 0xff), (int)(firstChroma >> 16), bottomDestination[((evenPixel - 1) * bytesPerPixel)..]);

                    ConvertPixel(
                        bottomY[evenPixel], (int)(secondChroma & 0xff), (int)(secondChroma >> 16), bottomDestination[(evenPixel * bytesPerPixel)..]);
                }

                topLeft = topRight;
                bottomLeft = bottomRight;
            }

            // An even row width has one final pixel without a right chroma neighbor. Replicate that edge.
            if ((length & 1) == 0)
            {
                // Repeating the missing right samples reduces the 9:3:3:1 filter to a 3:1 vertical blend.
                // topLeft and bottomLeft each hold U in bits 0..7 and V in bits 16..23.
                // For each channel, compute (3 * top + bottom + 2) / 4. The +2 rounds to the nearest byte code.
                // 0x00020002 adds that bias to both 16-bit words. Each sum fits in ten bits, so U cannot carry into V.
                // Shifting by two divides both sums by four. Discarded V bits enter the upper part of the U word;
                // & 0xff extracts only the U byte, while >> 16 extracts the V byte.
                firstChroma = ((3 * topLeft) + bottomLeft + 0x00020002u) >> 2;

                // Use this pixel's luma and write its three output bytes at the final pixel offset.
                ConvertPixel(topY[length - 1], (int)(firstChroma & 0xff), (int)(firstChroma >> 16), topDestination[((length - 1) * bytesPerPixel)..]);

                if (!bottomY.IsEmpty)
                {
                    // The bottom pixel is nearer the bottom chroma row, so reverse the 3:1 weights.
                    firstChroma = ((3 * bottomLeft) + topLeft + 0x00020002u) >> 2;
                    ConvertPixel(bottomY[length - 1], (int)(firstChroma & 0xff), (int)(firstChroma >> 16), bottomDestination[((length - 1) * bytesPerPixel)..]);
                }
            }
        }

        /// <summary>
        /// Processes complete SIMD blocks, then interpolates the remaining pixels with scalar arithmetic.
        /// </summary>
        /// <param name="topY">The first output row's luma samples.</param>
        /// <param name="bottomY">The second output row's luma samples, or empty for a single row.</param>
        /// <param name="topU">The preceding U chroma row.</param>
        /// <param name="topV">The preceding V chroma row.</param>
        /// <param name="curU">The current U chroma row.</param>
        /// <param name="curV">The current V chroma row.</param>
        /// <param name="topDestination">The first packed output row.</param>
        /// <param name="bottomDestination">The second packed output row, or empty for a single row.</param>
        /// <param name="length">The number of output pixels in each row.</param>
        /// <param name="uvBuffer">The decoder-owned scratch space for four 64-byte interpolated chroma planes.</param>
        private static void UpSampleVector(
            Span<byte> topY,
            Span<byte> bottomY,
            Span<byte> topU,
            Span<byte> topV,
            Span<byte> curU,
            Span<byte> curV,
            Span<byte> topDestination,
            Span<byte> bottomDestination,
            int length,
            Span<byte> uvBuffer)
        {
            const int bytesPerPixel = 3;

            // A 512-bit block produces 64 chroma bytes per output plane. Keep U and V separate for both rows;
            // the decoder owns these four 64-byte scratch planes, and smaller blocks reuse each plane's prefix.
            Span<byte> topUOutput = uvBuffer[..64];
            Span<byte> topVOutput = uvBuffer[64..128];
            Span<byte> bottomUOutput = uvBuffer[128..192];
            Span<byte> bottomVOutput = uvBuffer[192..256];

            // The first pixel uses the replicated left chroma edge from the scalar decoder.
            int uVerticalAverage = ((topU[0] + curU[0]) >> 1) + 1;
            int vVerticalAverage = ((topV[0] + curV[0]) >> 1) + 1;
            ConvertPixel(topY[0], (topU[0] + uVerticalAverage) >> 1, (topV[0] + vVerticalAverage) >> 1, topDestination);

            if (!bottomY.IsEmpty)
            {
                ConvertPixel(bottomY[0], (curU[0] + uVerticalAverage) >> 1, (curV[0] + vVerticalAverage) >> 1, bottomDestination);
            }

            int pixelOffset = 1;
            int chromaOffset = 0;
            int lastChromaPair = (length - 1) >> 1;
            ref byte topURef = ref MemoryMarshal.GetReference(topU);
            ref byte topVRef = ref MemoryMarshal.GetReference(topV);
            ref byte curURef = ref MemoryMarshal.GetReference(curU);
            ref byte curVRef = ref MemoryMarshal.GetReference(curV);

            // Each interpolation load also reads the next chroma sample. Excluding lastChromaPair from these counts
            // leaves that right neighbor inside the source span. A chroma pair produces two output pixels.
            // The 512-bit packer uses AVX512BW byte shuffles, so generic 512-bit acceleration alone is insufficient.
            if (Avx512BW.IsSupported)
            {
                // 32 unsigned word lanes interpolate 64 output pixels from consecutive chroma pairs.
                nuint vectorCount = topU[chromaOffset..lastChromaPair].Vector512Count<ushort>();

                for (nuint block = 0; block < vectorCount; block++)
                {
                    InterpolateChroma512(
                        ref Unsafe.Add(ref topURef, (uint)chromaOffset), ref Unsafe.Add(ref curURef, (uint)chromaOffset), topUOutput, bottomUOutput);

                    InterpolateChroma512(
                        ref Unsafe.Add(ref topVRef, (uint)chromaOffset), ref Unsafe.Add(ref curVRef, (uint)chromaOffset), topVOutput, bottomVOutput);

                    Convert64(topY[pixelOffset..], topUOutput, topVOutput, topDestination[(pixelOffset * bytesPerPixel)..]);

                    if (!bottomY.IsEmpty)
                    {
                        Convert64(bottomY[pixelOffset..], bottomUOutput, bottomVOutput, bottomDestination[(pixelOffset * bytesPerPixel)..]);
                    }

                    pixelOffset += Vector512<ushort>.Count * 2;
                    chromaOffset += Vector512<ushort>.Count;
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                // 16 unsigned word lanes interpolate 32 output pixels from consecutive chroma pairs.
                nuint vectorCount = topU[chromaOffset..lastChromaPair].Vector256Count<ushort>();

                for (nuint block = 0; block < vectorCount; block++)
                {
                    InterpolateChroma256(
                        ref Unsafe.Add(ref topURef, (uint)chromaOffset), ref Unsafe.Add(ref curURef, (uint)chromaOffset), topUOutput, bottomUOutput);

                    InterpolateChroma256(
                        ref Unsafe.Add(ref topVRef, (uint)chromaOffset), ref Unsafe.Add(ref curVRef, (uint)chromaOffset), topVOutput, bottomVOutput);

                    Convert32(topY[pixelOffset..], topUOutput, topVOutput, topDestination[(pixelOffset * bytesPerPixel)..]);

                    if (!bottomY.IsEmpty)
                    {
                        Convert32(bottomY[pixelOffset..], bottomUOutput, bottomVOutput, bottomDestination[(pixelOffset * bytesPerPixel)..]);
                    }

                    pixelOffset += Vector256<ushort>.Count * 2;
                    chromaOffset += Vector256<ushort>.Count;
                }
            }

            // Eight unsigned word lanes interpolate sixteen output pixels; the remaining pairs use scalar arithmetic.
            nuint vectorCount128 = topU[chromaOffset..lastChromaPair].Vector128Count<ushort>();

            for (nuint block = 0; block < vectorCount128; block++)
            {
                InterpolateChroma128(
                    ref Unsafe.Add(ref topURef, (uint)chromaOffset), ref Unsafe.Add(ref curURef, (uint)chromaOffset), topUOutput, bottomUOutput);

                InterpolateChroma128(
                    ref Unsafe.Add(ref topVRef, (uint)chromaOffset), ref Unsafe.Add(ref curVRef, (uint)chromaOffset), topVOutput, bottomVOutput);

                Convert16(topY[pixelOffset..], topUOutput, topVOutput, topDestination[(pixelOffset * bytesPerPixel)..]);

                if (!bottomY.IsEmpty)
                {
                    Convert16(bottomY[pixelOffset..], bottomUOutput, bottomVOutput, bottomDestination[(pixelOffset * bytesPerPixel)..]);
                }

                pixelOffset += Vector128<ushort>.Count * 2;
                chromaOffset += Vector128<ushort>.Count;
            }

            if (pixelOffset < length)
            {
                // Continue the scalar pair traversal from the last consumed chroma pair without rewriting output.
                uint topLeft = LoadUv(topU[chromaOffset], topV[chromaOffset]);
                uint bottomLeft = LoadUv(curU[chromaOffset], curV[chromaOffset]);

                for (int x = chromaOffset + 1; x <= lastChromaPair; x++)
                {
                    uint topRight = LoadUv(topU[x], topV[x]);
                    uint bottomRight = LoadUv(curU[x], curV[x]);

                    // Pack U and V into separate words; +8 in each word gives the same rounding as the SIMD filter.
                    // The sums fit within their words, so adding U never carries into V.
                    uint sum = topLeft + topRight + bottomLeft + bottomRight + 0x00080008u;
                    uint crossDiagonal = (sum + (2 * (topRight + bottomLeft))) >> 3;
                    uint mainDiagonal = (sum + (2 * (topLeft + bottomRight))) >> 3;
                    int evenPixel = x * 2;
                    uint topOdd = (crossDiagonal + topLeft) >> 1;
                    uint topEven = (mainDiagonal + topRight) >> 1;
                    ConvertPixel(topY[evenPixel - 1], (int)(topOdd & 0xff), (int)(topOdd >> 16), topDestination[((evenPixel - 1) * bytesPerPixel)..]);
                    ConvertPixel(topY[evenPixel], (int)(topEven & 0xff), (int)(topEven >> 16), topDestination[(evenPixel * bytesPerPixel)..]);

                    if (!bottomY.IsEmpty)
                    {
                        uint bottomOdd = (mainDiagonal + bottomLeft) >> 1;
                        uint bottomEven = (crossDiagonal + bottomRight) >> 1;
                        ConvertPixel(
                            bottomY[evenPixel - 1], (int)(bottomOdd & 0xff), (int)(bottomOdd >> 16), bottomDestination[((evenPixel - 1) * bytesPerPixel)..]);

                        ConvertPixel(bottomY[evenPixel], (int)(bottomEven & 0xff), (int)(bottomEven >> 16), bottomDestination[(evenPixel * bytesPerPixel)..]);
                    }

                    topLeft = topRight;
                    bottomLeft = bottomRight;
                }

                // The last pixel of an even-width row repeats the right chroma edge instead of reading past it.
                if ((length & 1) == 0)
                {
                    // Repeating the missing right samples reduces the 9:3:3:1 filter to a 3:1 vertical blend.
                    // topLeft and bottomLeft each hold U in bits 0..7 and V in bits 16..23.
                    // For each channel, compute (3 * top + bottom + 2) / 4. The +2 rounds to the nearest byte code.
                    // 0x00020002 adds that bias to both 16-bit words. Each sum fits in ten bits, so U cannot carry into V.
                    // Shifting by two divides both sums by four. Discarded V bits enter the upper part of the U word;
                    // & 0xff extracts only the U byte, while >> 16 extracts the V byte.
                    uint edgeTop = ((3 * topLeft) + bottomLeft + 0x00020002u) >> 2;

                    // Use this pixel's luma and write its three output bytes at the final pixel offset.
                    ConvertPixel(topY[length - 1], (int)(edgeTop & 0xff), (int)(edgeTop >> 16), topDestination[((length - 1) * bytesPerPixel)..]);

                    if (!bottomY.IsEmpty)
                    {
                        // The bottom pixel is nearer the bottom chroma row, so reverse the 3:1 weights.
                        uint edgeBottom = ((3 * bottomLeft) + topLeft + 0x00020002u) >> 2;
                        ConvertPixel(
                            bottomY[length - 1], (int)(edgeBottom & 0xff), (int)(edgeBottom >> 16), bottomDestination[((length - 1) * bytesPerPixel)..]);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the weighted chroma filter to thirty-two lanes using 512-bit word arithmetic.
        /// </summary>
        /// <param name="top">The first sample in the preceding chroma row, including the required right neighbor.</param>
        /// <param name="current">The first sample in the current chroma row, including the required right neighbor.</param>
        /// <param name="topOutput">The interpolated chroma bytes for the first output row.</param>
        /// <param name="bottomOutput">The interpolated chroma bytes for the second output row.</param>
        private static void InterpolateChroma512(ref byte top, ref byte current, Span<byte> topOutput, Span<byte> bottomOutput)
        {
            // Matching lane positions hold the four corners of one chroma neighborhood. Shifted loads select the right neighbors.
            Vector512<ushort> topLeft = Load32Chroma(ref top);

            Vector512<ushort> topRight = Load32Chroma(ref Unsafe.Add(ref top, 1U));
            Vector512<ushort> bottomLeft = Load32Chroma(ref current);
            Vector512<ushort> bottomRight = Load32Chroma(ref Unsafe.Add(ref current, 1U));
            Vector512<ushort> three = Vector512.Create((ushort)3);
            Vector512<ushort> nine = Vector512.Create((ushort)9);
            Vector512<ushort> roundingBias = Vector512.Create((ushort)8);

            // The nearest sample receives weight nine, the two adjacent samples weight three, and the diagonal weight one.
            // The weights sum to sixteen. Adding eight rounds the division; the largest sum is 4088, within ushort range.
            Vector512<ushort> topOdd = ((topLeft * nine) + (topRight * three) + (bottomLeft * three) + bottomRight + roundingBias) >> 4;
            Vector512<ushort> topEven = ((topLeft * three) + (topRight * nine) + bottomLeft + (bottomRight * three) + roundingBias) >> 4;
            Vector512<ushort> bottomOdd = ((topLeft * three) + topRight + (bottomLeft * nine) + (bottomRight * three) + roundingBias) >> 4;
            Vector512<ushort> bottomEven = (topLeft + (topRight * three) + (bottomLeft * three) + (bottomRight * nine) + roundingBias) >> 4;

            // Store the odd pixel before the even pixel so the chroma plane follows the luma row's pixel order.
            StoreInterleaved(topOdd, topEven, topOutput);
            StoreInterleaved(bottomOdd, bottomEven, bottomOutput);
        }

        /// <summary>
        /// Applies the weighted chroma filter to sixteen lanes using 256-bit word arithmetic.
        /// </summary>
        /// <param name="top">The first sample in the preceding chroma row, including the required right neighbor.</param>
        /// <param name="current">The first sample in the current chroma row, including the required right neighbor.</param>
        /// <param name="topOutput">The interpolated chroma bytes for the first output row.</param>
        /// <param name="bottomOutput">The interpolated chroma bytes for the second output row.</param>
        private static void InterpolateChroma256(ref byte top, ref byte current, Span<byte> topOutput, Span<byte> bottomOutput)
        {
            // Matching lane positions hold the four corners of one chroma neighborhood. Shifted loads select the right neighbors.
            Vector256<ushort> topLeft = Load16Chroma(ref top);

            Vector256<ushort> topRight = Load16Chroma(ref Unsafe.Add(ref top, 1U));
            Vector256<ushort> bottomLeft = Load16Chroma(ref current);
            Vector256<ushort> bottomRight = Load16Chroma(ref Unsafe.Add(ref current, 1U));
            Vector256<ushort> three = Vector256.Create((ushort)3);
            Vector256<ushort> nine = Vector256.Create((ushort)9);
            Vector256<ushort> roundingBias = Vector256.Create((ushort)8);

            // The nearest sample receives weight nine, the two adjacent samples weight three, and the diagonal weight one.
            // The weights sum to sixteen. Adding eight rounds the division; the largest sum is 4088, within ushort range.
            Vector256<ushort> topOdd = ((topLeft * nine) + (topRight * three) + (bottomLeft * three) + bottomRight + roundingBias) >> 4;
            Vector256<ushort> topEven = ((topLeft * three) + (topRight * nine) + bottomLeft + (bottomRight * three) + roundingBias) >> 4;
            Vector256<ushort> bottomOdd = ((topLeft * three) + topRight + (bottomLeft * nine) + (bottomRight * three) + roundingBias) >> 4;
            Vector256<ushort> bottomEven = (topLeft + (topRight * three) + (bottomLeft * three) + (bottomRight * nine) + roundingBias) >> 4;

            // Store the odd pixel before the even pixel so the chroma plane follows the luma row's pixel order.
            StoreInterleaved(topOdd, topEven, topOutput);
            StoreInterleaved(bottomOdd, bottomEven, bottomOutput);
        }

        /// <summary>
        /// Applies the weighted chroma filter to eight lanes using 128-bit word arithmetic.
        /// </summary>
        /// <param name="top">The first sample in the preceding chroma row, including the required right neighbor.</param>
        /// <param name="current">The first sample in the current chroma row, including the required right neighbor.</param>
        /// <param name="topOutput">The interpolated chroma bytes for the first output row.</param>
        /// <param name="bottomOutput">The interpolated chroma bytes for the second output row.</param>
        private static void InterpolateChroma128(ref byte top, ref byte current, Span<byte> topOutput, Span<byte> bottomOutput)
        {
            // Matching lane positions hold the four corners of one chroma neighborhood. Shifted loads select the right neighbors.
            Vector128<ushort> topLeft = Load8Chroma(ref top);

            Vector128<ushort> topRight = Load8Chroma(ref Unsafe.Add(ref top, 1U));
            Vector128<ushort> bottomLeft = Load8Chroma(ref current);
            Vector128<ushort> bottomRight = Load8Chroma(ref Unsafe.Add(ref current, 1U));
            Vector128<ushort> three = Vector128.Create((ushort)3);
            Vector128<ushort> nine = Vector128.Create((ushort)9);
            Vector128<ushort> roundingBias = Vector128.Create((ushort)8);

            // The nearest sample receives weight nine, the two adjacent samples weight three, and the diagonal weight one.
            // The weights sum to sixteen. Adding eight rounds the division; the largest sum is 4088, within ushort range.
            Vector128<ushort> topOdd = ((topLeft * nine) + (topRight * three) + (bottomLeft * three) + bottomRight + roundingBias) >> 4;
            Vector128<ushort> topEven = ((topLeft * three) + (topRight * nine) + bottomLeft + (bottomRight * three) + roundingBias) >> 4;
            Vector128<ushort> bottomOdd = ((topLeft * three) + topRight + (bottomLeft * nine) + (bottomRight * three) + roundingBias) >> 4;
            Vector128<ushort> bottomEven = (topLeft + (topRight * three) + (bottomLeft * three) + (bottomRight * nine) + roundingBias) >> 4;

            // Store the odd pixel before the even pixel so the chroma plane follows the luma row's pixel order.
            StoreInterleaved(topOdd, topEven, topOutput);
            StoreInterleaved(bottomOdd, bottomEven, bottomOutput);
        }

        /// <summary>
        /// Widens thirty-two byte chroma codes into unsigned word lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector512<ushort> Load32Chroma(ref byte source)
        {
            Vector256<byte> bytes = Unsafe.ReadUnaligned<Vector256<byte>>(ref source);
            (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(bytes);
            return Vector512.Create(lower, upper);
        }

        /// <summary>
        /// Widens sixteen byte chroma codes into unsigned word lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector256<ushort> Load16Chroma(ref byte source)
        {
            Vector128<byte> bytes = Unsafe.ReadUnaligned<Vector128<byte>>(ref source);
            (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(bytes);
            return Vector256.Create(lower, upper);
        }

        /// <summary>
        /// Widens eight byte chroma codes into unsigned word lanes.
        /// </summary>
        /// <param name="source">The first source byte; the caller supplies all samples required by this load.</param>
        /// <returns>The loaded byte codes widened into consecutive lanes.</returns>
        private static Vector128<ushort> Load8Chroma(ref byte source)
        {
            Vector128<byte> bytes = Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte();
            (Vector128<ushort> lower, _) = Vector128.Widen(bytes);
            return lower;
        }

        /// <summary>
        /// Interleaves two thirty-two-lane results into sixty-four chroma bytes in pixel order.
        /// </summary>
        /// <param name="odd">The byte-range samples for odd pixel positions, stored in word lanes.</param>
        /// <param name="even">The byte-range samples for even pixel positions, stored in word lanes.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void StoreInterleaved(Vector512<ushort> odd, Vector512<ushort> even, Span<byte> destination)
        {
            // Both values fit in one byte. Place the odd pixel in the low byte and the even pixel in the high byte;
            // little-endian storage then writes each pair in pixel order without a narrowing shuffle.
            Vector512<ushort> pairs = odd | (even << 8);

            Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(destination)) = pairs.AsByte();
        }

        /// <summary>
        /// Interleaves two sixteen-lane results into thirty-two chroma bytes in pixel order.
        /// </summary>
        /// <param name="odd">The byte-range samples for odd pixel positions, stored in word lanes.</param>
        /// <param name="even">The byte-range samples for even pixel positions, stored in word lanes.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void StoreInterleaved(Vector256<ushort> odd, Vector256<ushort> even, Span<byte> destination)
        {
            // Both values fit in one byte. Place the odd pixel in the low byte and the even pixel in the high byte;
            // little-endian storage then writes each pair in pixel order without a narrowing shuffle.
            Vector256<ushort> pairs = odd | (even << 8);

            Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(destination)) = pairs.AsByte();
        }

        /// <summary>
        /// Interleaves two eight-lane results into sixteen chroma bytes in pixel order.
        /// </summary>
        /// <param name="odd">The byte-range samples for odd pixel positions, stored in word lanes.</param>
        /// <param name="even">The byte-range samples for even pixel positions, stored in word lanes.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void StoreInterleaved(Vector128<ushort> odd, Vector128<ushort> even, Span<byte> destination)
        {
            // Both values fit in one byte. Place the odd pixel in the low byte and the even pixel in the high byte;
            // little-endian storage then writes each pair in pixel order without a narrowing shuffle.
            Vector128<ushort> pairs = odd | (even << 8);

            Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination)) = pairs.AsByte();
        }

        /// <summary>
        /// Interleaves three 64-byte planes into three packed 64-byte blocks.
        /// </summary>
        /// <param name="c0">The first source channel plane.</param>
        /// <param name="c1">The second source channel plane.</param>
        /// <param name="c2">The third source channel plane.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void PlanarTo24b(Vector512<byte> c0, Vector512<byte> c1, Vector512<byte> c2, Span<byte> destination)
        {
            // The caller requires AVX512BW. Select whole 128-bit source lanes with a 512-bit int shuffle first;
            // the BW byte shuffle can then interleave within each lane without requiring AVX512VBMI.
            for (int block = 0; block < 3; block++)
            {
                int maskIndex = block * 3;
                Vector512<int> laneIndices = PlanarLaneIndices512[block];
                Vector512<byte> plane0 = Vector512.Shuffle(c0.AsInt32(), laneIndices).AsByte();
                Vector512<byte> plane1 = Vector512.Shuffle(c1.AsInt32(), laneIndices).AsByte();
                Vector512<byte> plane2 = Vector512.Shuffle(c2.AsInt32(), laneIndices).AsByte();

                // BW uses each index's low four bits within its lane and zeros indices with the high bit set.
                // Thus the full-plane byte masks also select the right byte after the source lanes are positioned.
                Vector512<byte> packed = Avx512BW.Shuffle(plane0, PlanarMasks512[maskIndex])
                    | Avx512BW.Shuffle(plane1, PlanarMasks512[maskIndex + 1])
                    | Avx512BW.Shuffle(plane2, PlanarMasks512[maskIndex + 2]);

                packed.StoreUnsafe(ref MemoryMarshal.GetReference(destination), (nuint)(block * Vector512<byte>.Count));
            }
        }

        /// <summary>
        /// Interleaves three 32-byte planes into three packed 32-byte blocks.
        /// </summary>
        /// <param name="c0">The first source channel plane.</param>
        /// <param name="c1">The second source channel plane.</param>
        /// <param name="c2">The third source channel plane.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void PlanarTo24b(Vector256<byte> c0, Vector256<byte> c1, Vector256<byte> c2, Span<byte> destination)
        {
            // Three source planes produce three full output vectors. Each shuffle reads the full source plane
            // and zeros the other two channels, so OR combines them into consecutive three-byte pixels.
            for (int block = 0; block < 3; block++)
            {
                int maskIndex = block * 3;
                Vector256<byte> packed = Vector256.Shuffle(c0, PlanarMasks256[maskIndex])
                    | Vector256.Shuffle(c1, PlanarMasks256[maskIndex + 1])
                    | Vector256.Shuffle(c2, PlanarMasks256[maskIndex + 2]);

                packed.StoreUnsafe(ref MemoryMarshal.GetReference(destination), (nuint)(block * Vector256<byte>.Count));
            }
        }

        /// <summary>
        /// Interleaves three 16-byte planes into three packed 16-byte blocks.
        /// </summary>
        /// <param name="c0">The first source channel plane.</param>
        /// <param name="c1">The second source channel plane.</param>
        /// <param name="c2">The third source channel plane.</param>
        /// <param name="destination">The packed output bytes in pixel order.</param>
        private static void PlanarTo24b(Vector128<byte> c0, Vector128<byte> c1, Vector128<byte> c2, Span<byte> destination)
        {
            // Three source planes produce three full output vectors. Each shuffle reads the full source plane
            // and zeros the other two channels, so OR combines them into consecutive three-byte pixels.
            for (int block = 0; block < 3; block++)
            {
                int maskIndex = block * 3;
                Vector128<byte> packed = Vector128.Shuffle(c0, PlanarMasks128[maskIndex])
                    | Vector128.Shuffle(c1, PlanarMasks128[maskIndex + 1])
                    | Vector128.Shuffle(c2, PlanarMasks128[maskIndex + 2]);

                packed.StoreUnsafe(ref MemoryMarshal.GetReference(destination), (nuint)(block * Vector128<byte>.Count));
            }
        }

        /// <inheritdoc />
        public override void ConvertRgbRow<TPixel, TSourcePixel>(
            Configuration configuration,
            ReadOnlySpan<TSourcePixel> source,
            ColorProfileConverter? converter,
            Span<Vector4> vectors,
            Span<Rgb> rgb,
            Span<TPixel> destination)
        {
            if (converter is null)
            {
                PixelOperations<TPixel>.Instance.From(configuration, source, destination);
                return;
            }

            // The source pixel type supplies its alpha representation. Conversion leaves straight RGB for destination packing.
            Span<Vector4> row = vectors[..source.Length];
            converter.Convert(configuration, source, row, rgb[..source.Length]);
            PackIccRgbRow(configuration, row, ReadOnlySpan<byte>.Empty, destination);
        }

        /// <inheritdoc />
        public override void ConvertYuvRow<TPixel>(
            Configuration configuration,
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> alpha,
            ColorProfileConverter? converter,
            Span<Vector4> vectors,
            Span<Rgb> rgb,
            Span<TPixel> destination)
        {
            if (converter is null)
            {
                if (alpha.IsEmpty)
                {
                    PixelOperations<TPixel>.Instance.FromBgr24Bytes(configuration, source, destination, destination.Length);
                    return;
                }

                // This 256-byte stack block avoids a row allocation, and the bulk destination packer owns alpha association.
                InlineArray64<Bgra32> pixelsBuffer = default;
                Span<Bgra32> pixels = pixelsBuffer;
                int offset = 0;

                while (offset < destination.Length)
                {
                    int count = Math.Min(pixels.Length, destination.Length - offset);
                    Span<Bgra32> block = pixels[..count];

                    // The existing Pad3Shuffle4 path expands BGR before JoinAlpha replaces each padding byte.
                    PixelConverter.FromBgr24.ToBgra32(source.Slice(offset * 3, count * 3), MemoryMarshal.AsBytes(block));
                    JoinAlpha(block, alpha.Slice(offset, count));
                    PixelOperations<TPixel>.Instance.FromBgra32(configuration, block, destination.Slice(offset, count));
                    offset += count;
                }

                return;
            }

            // Source bytes are BGR or YCbCr, selected before decoding. Separate lossy alpha is joined only after ICC.
            Span<Vector4> row = vectors[..destination.Length];
            TOperator.ConvertSamplesWithIcc(configuration, source[..(destination.Length * 3)], converter, row, rgb[..destination.Length]);
            PackIccRgbRow(configuration, row, alpha, destination);
        }

        /// <summary>
        /// Replaces each padded BGR pixel's high byte with its separate alpha sample.
        /// </summary>
        /// <param name="pixels">The expanded B,G,R,unused-byte pixels.</param>
        /// <param name="alpha">The matching unassociated alpha bytes.</param>
        private static void JoinAlpha(Span<Bgra32> pixels, ReadOnlySpan<byte> alpha)
        {
            ref Bgra32 pixelsBase = ref MemoryMarshal.GetReference(pixels);
            ref uint pixelValues = ref Unsafe.As<Bgra32, uint>(ref pixelsBase);
            ref byte alphaBase = ref MemoryMarshal.GetReference(alpha);
            int i = 0;

            // Each uint stores B,G,R,A on little-endian systems. The shift moves straight alpha into bits 24-31, and the mask preserves RGB.
            // The destination packer handles alpha association after this merge.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = alpha.Vector512Count<uint>();

                for (nuint block = 0; block < vectorCount; block++)
                {
                    int offset = (int)(block * (uint)Vector512<uint>.Count);
                    Vector512<uint> alphaValues = Load16(ref Unsafe.Add(ref alphaBase, (uint)offset)).AsUInt32() << 24;
                    ref Vector512<uint> pixel = ref Unsafe.As<uint, Vector512<uint>>(ref Unsafe.Add(ref pixelValues, (uint)offset));

                    pixel = Vector512.ConditionalSelect(Vector512.Create(0xFF000000U), alphaValues, pixel);
                }

                i = (int)(vectorCount * (uint)Vector512<uint>.Count);
            }

            // This tier widens bytes to eight integer lanes and merges eight packed BGRA pixels at a time.
            if (Avx2.IsSupported)
            {
                nuint vectorCount = alpha[i..].Vector256Count<uint>();

                for (nuint block = 0; block < vectorCount; block++)
                {
                    int offset = i + (int)(block * (uint)Vector256<uint>.Count);
                    Vector256<uint> alphaValues = Load8(ref Unsafe.Add(ref alphaBase, (uint)offset)).AsUInt32() << 24;
                    ref Vector256<uint> pixel = ref Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref pixelValues, (uint)offset));
                    pixel = Vector256.ConditionalSelect(Vector256.Create(0xFF000000U), alphaValues, pixel);
                }

                i += (int)(vectorCount * (uint)Vector256<uint>.Count);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = alpha[i..].Vector128Count<uint>();

                for (nuint block = 0; block < vectorCount; block++)
                {
                    int offset = i + (int)(block * (uint)Vector128<uint>.Count);
                    Vector128<uint> alphaValues = Load4(ref Unsafe.Add(ref alphaBase, (uint)offset)).AsUInt32() << 24;
                    ref Vector128<uint> pixel = ref Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref pixelValues, (uint)offset));
                    pixel = Vector128.ConditionalSelect(Vector128.Create(0xFF000000U), alphaValues, pixel);
                }

                i += (int)(vectorCount * (uint)Vector128<uint>.Count);
            }

            // The scalar tail handles pixels that do not fill a 128-bit block.
            for (; i < alpha.Length; i++)
            {
                pixels[i].A = alpha[i];
            }
        }
    }
}
