// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

internal static class JxlAnsEncoder
{
    public static void StoreVarLenUInt8(int n, JxlBitWriter writer)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 255, nameof(n));

        // vl_uint8(n) {
        //   /* n limited to 0..255 */
        //   is_nonzero_flag == B(1)
        //   if (is_nonzero_flag == 0) {
        //     /* Value is zero. */
        //     return 0;
        //   } else {
        //     vl_suffix_length = B(3)
        //     vl_suffix = B(vl_suffix_length)
        //     return vl_suffix_length
        //   }
        // }
        if (n == 0)
        {
            writer.Write(1, 0);
        }
        else
        {
            writer.Write(1, 1);
            int nbits = JxlMath.FloorLog2Nonzero(n);
            writer.Write(3, nbits);
            writer.Write(nbits, n - (1 << nbits));
        }
    }

    public static void StoreVarLenUInt16(int n, JxlBitWriter writer)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 65535, nameof(n));

        // vl_uint8(n) {
        //   /* n limited to 0..65535 */
        //   is_nonzero_flag == B(1)
        //   if (is_nonzero_flag == 0) {
        //     /* Value is zero. */
        //     return 0;
        //   } else {
        //     vl_suffix_length = B(4)
        //     vl_suffix = B(vl_suffix_length)
        //     return vl_suffix_length
        //   }
        // }
        if (n == 0)
        {
            writer.Write(1, 0);
        }
        else
        {
            writer.Write(1, 1);
            int nbits = JxlMath.FloorLog2Nonzero(n);
            writer.Write(4, nbits);
            writer.Write(nbits, n - (1 << nbits));
        }
    }
}
