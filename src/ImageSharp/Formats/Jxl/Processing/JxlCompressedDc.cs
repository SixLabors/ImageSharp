// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlCompressedDc
{
    private const float W1 = 0.20345139757231578f;
    private const float W2 = 0.0334829185968739f;
    private const float W0 = 1.0f - (4.0f * (W1 + W2));

    private static unsafe void ComputePixelChannelPacked(float dcFactor, Span<float> rowTop, Span<float> row, Span<float> rowBottom, out Vector<float> mc, out Vector<float> sm, out Vector<float> gap, int x)
    {
        // Use ptr for aligned vector loads
        fixed (float* pRowTop = rowTop)
        {
            fixed (float* pRow = row)
            {
                fixed (float* pRowBottom = rowBottom)
                {
                    // libjxl seems to use the unaligned and aligned
                    // vector loads in a specific pattern here.
                    // Additionally, use nontemporal writes as we only
                    // iterate through parts of the image once.
                    Vector<float> tl = Vector.Load(pRowTop + x - 1);
                    Vector<float> tc = Vector.LoadAlignedNonTemporal(pRowTop + x);
                    Vector<float> tr = Vector.Load(pRowTop + x + 1);

                    // TODO
                }
            }
        }
    }
}
