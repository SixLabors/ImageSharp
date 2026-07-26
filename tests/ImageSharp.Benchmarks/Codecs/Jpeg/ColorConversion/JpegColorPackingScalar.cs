// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Preserves the scalar JPEG packing loops that preceded the SIMD investigation.
/// </summary>
internal static class JpegColorPackingScalar
{
    /// <summary>
    /// Normalizes and interleaves three planar component lanes using the previous scalar implementation.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZ triples.</param>
    /// <param name="scale">The normalization factor applied to every component.</param>
    public static void PackedNormalizeInterleave3(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, Span<float> packed, float scale)
    {
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint packedOffset = i * 3;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, i) * scale;
        }
    }

    /// <summary>
    /// Deinterleaves packed XYZ values using the previous scalar implementation.
    /// </summary>
    /// <param name="packed">The source ordered as consecutive XYZ triples.</param>
    /// <param name="xLane">The destination X components.</param>
    /// <param name="yLane">The destination Y components.</param>
    /// <param name="zLane">The destination Z components.</param>
    public static void UnpackDeinterleave3(ReadOnlySpan<Vector3> packed, Span<float> xLane, Span<float> yLane, Span<float> zLane)
    {
        ref float packedRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);

        for (nuint i = 0; i < (nuint)packed.Length; i++)
        {
            nuint packedOffset = i * 3;
            Unsafe.Add(ref xLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset);
            Unsafe.Add(ref yLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 1);
            Unsafe.Add(ref zLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 2);
        }
    }

    /// <summary>
    /// Normalizes and interleaves four planar component lanes using the previous scalar implementation.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="wLane">The planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZW groups.</param>
    /// <param name="maximumValue">The maximum component value used to normalize each component.</param>
    public static void PackedNormalizeInterleave4(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, ReadOnlySpan<float> wLane, Span<float> packed, float maximumValue)
    {
        float scale = 1F / maximumValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint packedOffset = i * 4;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = Unsafe.Add(ref wLaneRef, i) * scale;
        }
    }

    /// <summary>
    /// Inverts, normalizes, and interleaves four planar lanes using the previous scalar implementation.
    /// </summary>
    /// <param name="xLane">The inverted planar X components.</param>
    /// <param name="yLane">The inverted planar Y components.</param>
    /// <param name="zLane">The inverted planar Z components.</param>
    /// <param name="wLane">The inverted planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive conventional XYZW groups.</param>
    /// <param name="maximumValue">The maximum component value used for inversion and normalization.</param>
    public static void PackedInvertNormalizeInterleave4(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, ReadOnlySpan<float> wLane, Span<float> packed, float maximumValue)
    {
        float scale = 1F / maximumValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint packedOffset = i * 4;
            Unsafe.Add(ref packedRef, packedOffset) = (maximumValue - Unsafe.Add(ref xLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = (maximumValue - Unsafe.Add(ref yLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = (maximumValue - Unsafe.Add(ref zLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = (maximumValue - Unsafe.Add(ref wLaneRef, i)) * scale;
        }
    }
}
