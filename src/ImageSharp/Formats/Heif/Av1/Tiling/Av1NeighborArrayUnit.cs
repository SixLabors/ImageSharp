// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1NeighborArrayUnit<T>
    where T : struct, IMinMaxValue<T>
{
    public static readonly T InvalidNeighborData = T.MaxValue;

    private readonly T[] left;
    private readonly T[] top;
    private readonly T[] topLeft;

    public Av1NeighborArrayUnit(int leftSize, int topSize, int topLeftSize)
    {
        this.left = new T[leftSize];
        this.top = new T[topSize];
        this.topLeft = new T[topLeftSize];
    }

    [Flags]
    public enum UnitMask
    {
        Left = 1,
        Top = 2,
        TopLeft = 4,
    }

    public Span<T> Left => this.left;

    public Span<T> Top => this.top;

    public Span<T> TopLeft => this.topLeft;

    public required int GranularityNormalLog2 { get; set; }

    public required int GranularityTopLeftLog2 { get; set; }

    public int UnitSize { get; private set; }

    public int GetLeftIndex(Point loc) => loc.Y >> this.GranularityNormalLog2;

    public int GetTopIndex(Point loc) => loc.X >> this.GranularityNormalLog2;

    public int GetTopLeftIndex(Point loc)
        => this.left.Length + (loc.X >> this.GranularityTopLeftLog2) - (loc.Y >> this.GranularityTopLeftLog2);

    public void UnitModeWrite(Span<T> value, Point origin, Size blockSize, UnitMask mask)
    {
        int idx, j;

        int count;
        int na_offset;
        int na_unit_size;

        na_unit_size = this.UnitSize;

        if ((mask & UnitMask.Top) == UnitMask.Top)
        {
            // Top Neighbor Array
            //     ----------12345678---------------------
            //                ^    ^
            //                |    |
            //                |    |
            //               xxxxxxxx
            //               x      x
            //               x      x
            //               12345678
            //
            //  The top neighbor array is updated with the samples from the
            //    bottom row of the source block
            //
            //  Index = org_x
            na_offset = this.GetTopIndex(origin);

            ref T dst_ptr = ref this.Top[na_offset * na_unit_size];

            count = blockSize.Width >> this.GranularityNormalLog2;

            for (idx = 0; idx < count; ++idx)
            {
                /* svt_memcpy less that 10 bytes*/
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
        }

        if ((mask & UnitMask.Left) == UnitMask.Left)
        {
            // Left Neighbor Array
            //
            //    |
            //    |
            //    1         xxxxxxx1
            //    2  <----  x      2
            //    3  <----  x      3
            //    4         xxxxxxx4
            //    |
            //    |
            //
            //  The left neighbor array is updated with the samples from the
            //    right column of the source block
            //
            //  Index = org_y
            na_offset = this.GetLeftIndex(origin);

            ref T dst_ptr = ref this.Left[na_offset * na_unit_size];

            count = blockSize.Height >> this.GranularityNormalLog2;

            for (idx = 0; idx < count; ++idx)
            {
                /* svt_memcpy less that 10 bytes*/
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
        }

        if ((mask & UnitMask.TopLeft) == UnitMask.TopLeft)
        {
            // Top-left Neighbor Array
            //
            //    4-5--6--7------------
            //    3 \      \
            //    2  \      \
            //    1   \      \
            //    |\   xxxxxx7
            //    | \  x     6
            //    |  \ x     5
            //    |   \1x2x3x4
            //    |
            //
            //  The top-left neighbor array is updated with the reversed samples
            //    from the right column and bottom row of the source block
            //
            // Index = org_x - org_y
            Point topLeft = origin;
            topLeft.Offset(0, blockSize.Height - 1);
            na_offset = this.GetTopLeftIndex(topLeft);

            // Copy bottom-row + right-column
            // *Note - start from the bottom-left corner
            ref T dst_ptr = ref this.TopLeft[na_offset * na_unit_size];

            count = ((blockSize.Width + blockSize.Height) >> this.GranularityTopLeftLog2) - 1;

            for (idx = 0; idx < count; ++idx)
            {
                /* svt_memcpy less that 10 bytes*/
                for (j = 0; j < na_unit_size; ++j)
                {
                    dst_ptr = value[j];
                    dst_ptr = Unsafe.Add(ref dst_ptr, 1);
                }
            }
        }
    }
}
