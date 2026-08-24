// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Normalizes three planar component lanes and interleaves them into packed XYZ values.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZ triples.</param>
    /// <param name="scale">The normalization factor applied to every component.</param>
    public static void PackedNormalizeInterleave3(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, Span<float> packed, float scale)
    {
        DebugGuard.IsTrue(packed.Length % 3 == 0, "Packed length must be divisible by 3.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 3, xLane.Length, nameof(packed));

        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = xLane.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // Each source vector contains four consecutive samples from one plane:
                //   x = [X0 X1 X2 X3]
                //   y = [Y0 Y1 Y2 Y3]
                //   z = [Z0 Z1 Z2 Z3]
                // Shifting X by one sample supplies the value that follows each XYZ triple:
                //   shiftedX = [X1 X2 X3 0]
                // The transpose therefore produces overlapping rows [Xn Yn Zn Xn+1].
                // AlignRight joins those rows into three complete destination vectors, avoiding
                // the scalar-sized stores that writing four independent Vector3 values requires.
                Vector128<float> x = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i)) * scaleVector;
                Vector128<float> y = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i)) * scaleVector;
                Vector128<float> z = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i)) * scaleVector;
                Vector128<float> shiftedX = Vector128_.ShiftRightBytesInVector(x.AsByte(), sizeof(float)).AsSingle();

                Transpose4(x, y, z, shiftedX, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

                // Dropping pixel2.X lets [Y2] complete [Y1 Z1 X2] from pixel1.
                Vector128<byte> shiftedPixel2 = Vector128_.ShiftRightBytesInVector(pixel2.AsByte(), sizeof(float));
                Vector128<float> packed1 = Vector128_.AlignRight(shiftedPixel2, pixel1.AsByte(), sizeof(float)).AsSingle();

                // Dropping pixel3.X leaves [Y3 Z3] to complete [Z2 X3] from pixel2.
                Vector128<byte> shiftedPixel3 = Vector128_.ShiftRightBytesInVector(pixel3.AsByte(), sizeof(float));
                Vector128<float> packed2 = Vector128_.AlignRight(shiftedPixel3, pixel2.AsByte(), sizeof(float) * 2).AsSingle();

                ref Vector128<float> destination = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, (uint)i * 3));

                destination = pixel0;
                Unsafe.Add(ref destination, 1) = packed1;
                Unsafe.Add(ref destination, 2) = packed2;
            }
        }

        // Fewer than four pixels remain after SIMD, or every pixel reaches this path
        // when the runtime cannot accelerate the cross-vector transpose.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 3;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, sourceOffset) * scale;
        }
    }

    /// <summary>
    /// Deinterleaves packed XYZ values into three planar component lanes.
    /// </summary>
    /// <param name="packed">The source ordered as consecutive XYZ triples.</param>
    /// <param name="xLane">The destination X components.</param>
    /// <param name="yLane">The destination Y components.</param>
    /// <param name="zLane">The destination Z components.</param>
    public static void UnpackDeinterleave3(ReadOnlySpan<Vector3> packed, Span<float> xLane, Span<float> yLane, Span<float> zLane)
    {
        DebugGuard.IsTrue(packed.Length == xLane.Length, nameof(packed), "Channels must be of same size!");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");

        ref float packedRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = packed.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // A Vector3 occupies twelve contiguous bytes, so a sixteen-byte load beginning
                // at one pixel also reads the X component of the following pixel:
                //   pixel0 = [X0 Y0 Z0 X1]
                //   pixel1 = [X1 Y1 Z1 X2]
                // The transpose discards this fourth column, making the overlap useful padding
                // and avoiding two insert instructions per pixel. The final row needs explicit
                // zero padding only when pixel3 is the last element in the source span.
                nuint packedOffset = (uint)i * 3;
                Vector128<float> pixel0 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, packedOffset));
                Vector128<float> pixel1 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, packedOffset + 3));
                Vector128<float> pixel2 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, packedOffset + 6));
                ref float pixel3Ref = ref Unsafe.Add(ref packedRef, packedOffset + 9);
                Vector128<float> pixel3 = i + Vector128<float>.Count < packed.Length ? Unsafe.As<float, Vector128<float>>(ref pixel3Ref) : Unsafe.As<float, Vector3>(ref pixel3Ref).AsVector128();

                Transpose4(pixel0, pixel1, pixel2, pixel3, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out _);

                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i)) = x;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i)) = y;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i)) = z;
            }
        }

        // The scalar remainder preserves the original scatter behavior for zero to
        // three pixels and provides the complete fallback on unsupported hardware.
        for (; i < packed.Length; i++)
        {
            nuint packedOffset = (uint)i * 3;
            Unsafe.Add(ref xLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset);
            Unsafe.Add(ref yLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 1);
            Unsafe.Add(ref zLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 2);
        }
    }

    /// <summary>
    /// Normalizes four planar component lanes and interleaves them into packed XYZW values.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="wLane">The planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZW groups.</param>
    /// <param name="maxValue">The maximum component value used to normalize each component.</param>
    public static void PackedNormalizeInterleave4(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, ReadOnlySpan<float> wLane, Span<float> packed, float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = xLane.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // Four planar vectors form the rows of a 4x4 matrix. Transposition
                // converts them into four complete [Xn Yn Zn Wn] pixel vectors, so
                // normalization and interleaving require only four loads, four
                // multiplies, the register transpose, and four contiguous stores.
                Vector128<float> x = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i)) * scaleVector;
                Vector128<float> y = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i)) * scaleVector;
                Vector128<float> z = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i)) * scaleVector;
                Vector128<float> w = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref wLaneRef, i)) * scaleVector;

                Transpose4(x, y, z, w, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

                ref Vector128<float> destination = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, (uint)i * 4));

                destination = pixel0;
                Unsafe.Add(ref destination, 1) = pixel1;
                Unsafe.Add(ref destination, 2) = pixel2;
                Unsafe.Add(ref destination, 3) = pixel3;
            }
        }

        // Process the zero-to-three trailing pixels with the same normalization
        // and component order as the vector transpose.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 4;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = Unsafe.Add(ref wLaneRef, sourceOffset) * scale;
        }
    }

    /// <summary>
    /// Inverts and normalizes four planar component lanes before interleaving them into packed XYZW values.
    /// </summary>
    /// <param name="xLane">The inverted planar X components.</param>
    /// <param name="yLane">The inverted planar Y components.</param>
    /// <param name="zLane">The inverted planar Z components.</param>
    /// <param name="wLane">The inverted planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive conventional XYZW groups.</param>
    /// <param name="maxValue">The maximum component value used for inversion and normalization.</param>
    public static void PackedInvertNormalizeInterleave4(ReadOnlySpan<float> xLane, ReadOnlySpan<float> yLane, ReadOnlySpan<float> zLane, ReadOnlySpan<float> wLane, Span<float> packed, float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> maximumVector = Vector128.Create(maxValue);
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = xLane.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // Adobe JPEG stores all four components inverted in the sample
                // domain. Reflecting and normalizing the planar vectors before the
                // transpose keeps both arithmetic operations lane-wise and leaves
                // the transpose responsible only for the planar-to-packed layout.
                Vector128<float> x = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i))) * scaleVector;
                Vector128<float> y = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i))) * scaleVector;
                Vector128<float> z = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i))) * scaleVector;
                Vector128<float> w = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref wLaneRef, i))) * scaleVector;

                Transpose4(x, y, z, w, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

                ref Vector128<float> destination = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, (uint)i * 4));

                destination = pixel0;
                Unsafe.Add(ref destination, 1) = pixel1;
                Unsafe.Add(ref destination, 2) = pixel2;
                Unsafe.Add(ref destination, 3) = pixel3;
            }
        }

        // Preserve the original operation order for the zero-to-three trailing pixels:
        // subtract in the sample domain, then multiply by the reciprocal maximum.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 4;
            Unsafe.Add(ref packedRef, packedOffset) = (maxValue - Unsafe.Add(ref xLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = (maxValue - Unsafe.Add(ref yLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = (maxValue - Unsafe.Add(ref zLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = (maxValue - Unsafe.Add(ref wLaneRef, sourceOffset)) * scale;
        }
    }

    /// <summary>
    /// Transposes four four-lane rows into four four-lane columns.
    /// </summary>
    /// <param name="row0">The first matrix row.</param>
    /// <param name="row1">The second matrix row.</param>
    /// <param name="row2">The third matrix row.</param>
    /// <param name="row3">The fourth matrix row.</param>
    /// <param name="column0">The first matrix column.</param>
    /// <param name="column1">The second matrix column.</param>
    /// <param name="column2">The third matrix column.</param>
    /// <param name="column3">The fourth matrix column.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose4(Vector128<float> row0, Vector128<float> row1, Vector128<float> row2, Vector128<float> row3, out Vector128<float> column0, out Vector128<float> column1, out Vector128<float> column2, out Vector128<float> column3)
    {
        // The first unpack interleaves adjacent 32-bit lanes from rows 0/1 and 2/3:
        //   row01Low = [r0c0 r1c0 r0c1 r1c1]
        //   row23Low = [r2c0 r3c0 r2c1 r3c1]
        // A second unpack treats each adjacent pair as one 64-bit lane and combines
        // the row01 and row23 pairs into complete columns. The integer views only
        // expose the cross-platform unpack helpers; every floating-point bit is preserved.
        Vector128<int> row01Low = Vector128_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector128<int> row01High = Vector128_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector128<int> row23Low = Vector128_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector128<int> row23High = Vector128_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector128_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector128_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector128_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector128_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }
}
