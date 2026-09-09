// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Converts normalized unassociated source components to sRGB before pixel packing.
    /// </summary>
    /// <param name="red">The normalized red or monochrome component row.</param>
    /// <param name="green">The normalized green component row.</param>
    /// <param name="blue">The normalized blue component row.</param>
    /// <param name="converter">The profile converter selected for this image region.</param>
    /// <param name="packed">The reusable interleaved RGB row.</param>
    public void ConvertRgbToSrgbInPlace(
        Span<float> red,
        Span<float> green,
        Span<float> blue,
        ColorProfileConverter converter,
        Span<Rgb> packed)
    {
        // A monochrome profile consumes one normalized channel. Color profiles consume the
        // RGB produced by the signaled H.273 operator, not the encoded YUV components.
        if (this.IsMonochrome)
        {
            // Limited-range samples can reconstruct outside the nominal interval. ICC device
            // curves address that interval, so clip before looking up their transfer functions.
            TensorPrimitives.Clamp(red, 0F, 1F, red);
            converter.Convert<Y, Rgb>(MemoryMarshal.Cast<float, Y>(red), packed);
        }
        else
        {
            Interleave3(red, green, blue, MemoryMarshal.Cast<Rgb, float>(packed));
            converter.Convert<Rgb, Rgb>(packed, packed);
        }

        UnpackDeinterleave3(MemoryMarshal.Cast<Rgb, Vector3>(packed), red, green, blue);
    }

    /// <summary>
    /// Interleaves three planar component lanes into packed XYZ values.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZ triples.</param>
    private static void Interleave3(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        Span<float> packed)
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
                Vector128<float> x = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i));
                Vector128<float> y = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i));
                Vector128<float> z = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i));

                // YUV reconstruction can overshoot the device RGB range. Clip while loading
                // the planes, before ICC curve lookup, without quantizing through packed pixels.
                x = Vector128.Clamp(x, Vector128<float>.Zero, Vector128.Create(1F));
                y = Vector128.Clamp(y, Vector128<float>.Zero, Vector128.Create(1F));
                z = Vector128.Clamp(z, Vector128<float>.Zero, Vector128.Create(1F));
                Vector128<float> shiftedX = Vector128_.ShiftRightBytesInVector(x.AsByte(), sizeof(float)).AsSingle();

                Transpose4(
                    x,
                    y,
                    z,
                    shiftedX,
                    out Vector128<float> pixel0,
                    out Vector128<float> pixel1,
                    out Vector128<float> pixel2,
                    out Vector128<float> pixel3);

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
            Unsafe.Add(ref packedRef, packedOffset) = Math.Clamp(Unsafe.Add(ref xLaneRef, sourceOffset), 0F, 1F);
            Unsafe.Add(ref packedRef, packedOffset + 1) = Math.Clamp(Unsafe.Add(ref yLaneRef, sourceOffset), 0F, 1F);
            Unsafe.Add(ref packedRef, packedOffset + 2) = Math.Clamp(Unsafe.Add(ref zLaneRef, sourceOffset), 0F, 1F);
        }
    }

    /// <summary>
    /// Deinterleaves packed XYZ values into three planar component lanes.
    /// </summary>
    /// <param name="packed">The source ordered as consecutive XYZ triples.</param>
    /// <param name="xLane">The destination X components.</param>
    /// <param name="yLane">The destination Y components.</param>
    /// <param name="zLane">The destination Z components.</param>
    private static void UnpackDeinterleave3(ReadOnlySpan<Vector3> packed, Span<float> xLane, Span<float> yLane, Span<float> zLane)
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
                Vector128<float> pixel3 = i + Vector128<float>.Count < packed.Length
                    ? Unsafe.As<float, Vector128<float>>(ref pixel3Ref)
                    : Unsafe.As<float, Vector3>(ref pixel3Ref).AsVector128();

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
    public static void Transpose4(
        Vector128<float> row0,
        Vector128<float> row1,
        Vector128<float> row2,
        Vector128<float> row3,
        out Vector128<float> column0,
        out Vector128<float> column1,
        out Vector128<float> column2,
        out Vector128<float> column3)
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
