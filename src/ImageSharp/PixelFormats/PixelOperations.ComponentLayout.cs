// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Shares register layout conversion for 16-bit pixel storage.
/// </content>
internal static partial class PixelOperations
{
    // These helpers move storage bits only. Numeric conversion and alpha rules belong to their calling pixel family.
    // Each expanded pixel occupies four ushort lanes: [R,G,B,A]. R and RG loads insert zero bits in absent lanes.
    // Callers restore the numeric defaults after conversion; a zero storage code does not always represent numeric zero.

    /// <summary>
    /// Loads two pixels and inserts zero bits for components absent from their storage.
    /// </summary>
    /// <param name="source">The first stored component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <param name="value">Two four-component groups in storage order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load16BitComponents(ref ushort source, nuint offset, int componentCount, out Vector128<ushort> value)
    {
        if (componentCount == 4)
        {
            value = Vector128.LoadUnsafe(ref source, offset);
            return;
        }

        ref byte start = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref source, offset));

        // Each RG pair occupies one uint. Widening it to ulong appends two empty component slots.
        // For R-only pixels, the first widening inserts G. Read only the two stored pixels, without padding.
        Vector128<uint> pairs = componentCount == 2
            ? Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref start)).AsUInt32()
            : Vector128.WidenLower(Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref start)).AsUInt16());

        value = Vector128.WidenLower(pairs).AsUInt16();
    }

    /// <summary>
    /// Loads four pixels and inserts zero bits for components absent from their storage.
    /// </summary>
    /// <param name="source">The first stored component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <param name="value">Four four-component groups in storage order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load16BitComponents(ref ushort source, nuint offset, int componentCount, out Vector256<ushort> value)
    {
        if (componentCount == 4)
        {
            value = Vector256.LoadUnsafe(ref source, offset);
            return;
        }

        ref byte start = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref source, offset));
        Vector128<uint> pairs = componentCount == 2
            ? Vector128.LoadUnsafe(ref source, offset).AsUInt32()
            : Vector128.WidenLower(Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref start)).AsUInt16());

        // Read 16 bytes for four RG pixels, or eight bytes for four R pixels. No load reads a fifth pixel.
        // Widening preserves each pair's low 32 bits and appends B/A slots. Both halves keep source pixel order.
        value = Vector256.Create(Vector128.WidenLower(pairs), Vector128.WidenUpper(pairs)).AsUInt16();
    }

    /// <summary>
    /// Loads eight pixels and inserts zero bits for components absent from their storage.
    /// </summary>
    /// <param name="source">The first stored component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <param name="value">Eight four-component groups in storage order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load16BitComponents(ref ushort source, nuint offset, int componentCount, out Vector512<ushort> value)
    {
        if (componentCount == 4)
        {
            value = Vector512.LoadUnsafe(ref source, offset);
            return;
        }

        Vector256<uint> pairs;
        if (componentCount == 2)
        {
            pairs = Vector256.LoadUnsafe(ref source, offset).AsUInt32();
        }
        else
        {
            Vector128<ushort> red = Vector128.LoadUnsafe(ref source, offset);
            pairs = Vector256.Create(Vector128.WidenLower(red), Vector128.WidenUpper(red));
        }

        // The input contains exactly eight R or RG pixels. Widen each pair to a four-component storage group.
        value = Vector512.Create(Vector256.WidenLower(pairs), Vector256.WidenUpper(pairs)).AsUInt16();
    }

    /// <summary>
    /// Stores two pixels, removing component slots that their format does not store.
    /// </summary>
    /// <param name="value">Two four-component groups after numeric packing.</param>
    /// <param name="destination">The first destination component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one through four.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store16BitComponents(Vector128<ushort> value, ref ushort destination, nuint offset, int componentCount)
    {
        if (componentCount == 4)
        {
            value.StoreUnsafe(ref destination, offset);
            return;
        }

        if (componentCount == 3)
        {
            // Two packed pixels contain [R0,G0,B0,A0,R1,G1,B1,A1]. Remove the two alpha words as byte pairs.
            // Native byte shuffling keeps each word's byte order. Zeroed high bytes are not written to the destination.
            Vector128<byte> rgb = Vector128.ShuffleNative(
                value.AsByte(),
                Vector128.Create((byte)0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 128, 128, 128, 128));

            // Write exactly twelve bytes. A full-register store would overwrite the next pixel or the end of the span.
            ref byte rgbStart = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destination, offset));
            Unsafe.WriteUnaligned(ref rgbStart, rgb.AsUInt64().ToScalar());
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref rgbStart, 8), rgb.AsUInt32().GetElement(2));
            return;
        }

        // Unsigned narrowing discards the unused high words. It does not saturate or change stored component bits.
        Vector128<uint> pairs = Vector128.Narrow(value.AsUInt64(), Vector128<ulong>.Zero);
        ref byte start = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destination, offset));
        if (componentCount == 2)
        {
            Unsafe.WriteUnaligned(ref start, pairs.AsUInt64().ToScalar());
        }
        else
        {
            Vector128<ushort> red = Vector128.Narrow(pairs, Vector128<uint>.Zero);
            Unsafe.WriteUnaligned(ref start, red.AsUInt32().ToScalar());
        }
    }

    /// <summary>
    /// Stores four pixels, removing component slots that their format does not store.
    /// </summary>
    /// <param name="value">Four four-component groups after numeric packing.</param>
    /// <param name="destination">The first destination component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one through four.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store16BitComponents(Vector256<ushort> value, ref ushort destination, nuint offset, int componentCount)
    {
        if (componentCount == 4)
        {
            value.StoreUnsafe(ref destination, offset);
            return;
        }

        if (componentCount == 3)
        {
            // Each half contains two complete RGBA pixels. Compact them into adjacent six-word RGB groups.
            Store16BitComponents(value.GetLower(), ref destination, offset, 3);
            Store16BitComponents(value.GetUpper(), ref destination, offset + 6, 3);
            return;
        }

        // Each ulong holds RGBA. Keep its low uint for RG, then keep the low ushort of each pair for R.
        Vector128<uint> pairs = Vector128.Narrow(value.GetLower().AsUInt64(), value.GetUpper().AsUInt64());
        if (componentCount == 2)
        {
            pairs.AsUInt16().StoreUnsafe(ref destination, offset);
        }
        else
        {
            Vector128<ushort> red = Vector128.Narrow(pairs, Vector128<uint>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destination, offset)), red.AsUInt64().ToScalar());
        }
    }

    /// <summary>
    /// Stores eight pixels, removing component slots that their format does not store.
    /// </summary>
    /// <param name="value">Eight four-component groups after numeric packing.</param>
    /// <param name="destination">The first destination component.</param>
    /// <param name="offset">The component offset of the first pixel.</param>
    /// <param name="componentCount">The stored component count: one through four.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store16BitComponents(Vector512<ushort> value, ref ushort destination, nuint offset, int componentCount)
    {
        if (componentCount == 4)
        {
            value.StoreUnsafe(ref destination, offset);
            return;
        }

        if (componentCount == 3)
        {
            // Each half contains four complete pixels. The compacted lower half occupies twelve RGB words.
            Store16BitComponents(value.GetLower(), ref destination, offset, 3);
            Store16BitComponents(value.GetUpper(), ref destination, offset + 12, 3);
            return;
        }

        // Narrow both halves in source order. The second narrowing is needed only for R storage.
        Vector256<uint> pairs = Vector256.Narrow(value.GetLower().AsUInt64(), value.GetUpper().AsUInt64());
        if (componentCount == 2)
        {
            pairs.AsUInt16().StoreUnsafe(ref destination, offset);
        }
        else
        {
            Vector128.Narrow(pairs.GetLower(), pairs.GetUpper()).StoreUnsafe(ref destination, offset);
        }
    }

    /// <summary>
    /// Supplies zero for absent RGB components and one for absent alpha after numeric conversion.
    /// </summary>
    /// <param name="value">One four-component pixel.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <returns>The pixel with its implicit component values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> RestoreImplicitComponents(Vector128<float> value, int componentCount)
    {
        if (componentCount == 4)
        {
            return value;
        }

        // All-one mask lanes retain the stored component bits, including signed zero and NaN payloads.
        // Other lanes select literal defaults instead of passing synthetic zero bits through the format's numeric map.
        Vector128<float> stored = Vector128.Create(-1, componentCount == 2 ? -1 : 0, 0, 0).AsSingle();
        return Vector128.ConditionalSelect(stored, value, Vector128.Create(0F, 0F, 0F, 1F));
    }

    /// <summary>
    /// Supplies implicit RGB and alpha values for two converted pixels.
    /// </summary>
    /// <param name="value">Two four-component pixels.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <returns>The pixels with their implicit component values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> RestoreImplicitComponents(Vector256<float> value, int componentCount)
    {
        if (componentCount == 4)
        {
            return value;
        }

        Vector128<float> stored = Vector128.Create(-1, componentCount == 2 ? -1 : 0, 0, 0).AsSingle();
        Vector128<float> defaults = Vector128.Create(0F, 0F, 0F, 1F);

        // Repeat the mask per pixel. Components from the second pixel never supply defaults for the first.
        return Vector256.ConditionalSelect(Vector256.Create(stored, stored), value, Vector256.Create(defaults, defaults));
    }

    /// <summary>
    /// Supplies implicit RGB and alpha values for four converted pixels.
    /// </summary>
    /// <param name="value">Four four-component pixels.</param>
    /// <param name="componentCount">The stored component count: one, two, or four.</param>
    /// <returns>The pixels with their implicit component values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> RestoreImplicitComponents(Vector512<float> value, int componentCount)
    {
        if (componentCount == 4)
        {
            return value;
        }

        Vector128<float> stored = Vector128.Create(-1, componentCount == 2 ? -1 : 0, 0, 0).AsSingle();
        Vector128<float> defaults = Vector128.Create(0F, 0F, 0F, 1F);
        Vector256<float> mask = Vector256.Create(stored, stored);
        Vector256<float> missing = Vector256.Create(defaults, defaults);

        // Four identical RGBA masks retain R or RG and supply literal B = 0 and A = 1 in each group.
        return Vector512.ConditionalSelect(Vector512.Create(mask, mask), value, Vector512.Create(missing, missing));
    }
}
