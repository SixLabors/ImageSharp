// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;

/// <summary>
/// Tracks reconstructed groups and assigns each ready interior, edge, and corner to one group.
/// </summary>
/// <remarks>
/// The frame decoder uses this owner sequentially. It clears affected groups before a new pass,
/// writes their samples, then marks each group complete. A returned rectangle has all required neighboring samples.
/// </remarks>
internal sealed class JxlGroupBorderAssigner : IDisposable
{
    /// <summary>
    /// The maximum number of ready rectangles returned for one completed group.
    /// </summary>
    public const int MaxToFinalize = 3;

    private readonly JxlFrameDimensions frameDim;
    private readonly Buffer2D<int> counters;

    private const byte TopLeft = 0x01;
    private const byte TopRight = 0x02;
    private const byte BottomRight = 0x04;
    private const byte BottomLeft = 0x08;

    /// <summary>
    /// Allocates completion counters for the parsed frame's group grid.
    /// </summary>
    /// <param name="configuration">The configuration that owns counter storage.</param>
    /// <param name="frameDim">The parsed pixel, block, and group dimensions.</param>
    public JxlGroupBorderAssigner(Configuration configuration, JxlFrameDimensions frameDim)
    {
        this.frameDim = frameDim;

        int xSize = frameDim.XSizeGroups;
        int ySize = frameDim.YSizeGroups;

        int counterCount = (xSize + 1 + 7) / 8;
        // Each corner has four completion bits, one for each touching group. Pack eight corners into one word.
        // The first buffer coordinate is the corner's Y position; the second selects its packed X word.
        // Interior corners must start empty. Only the image boundary starts with absent neighbors marked complete.
        this.counters = configuration.MemoryAllocator.Allocate2D<int>(ySize + 1, counterCount, AllocationOptions.Clean);

        void Set(int x, int y, uint corners)
        {
            int shift = 4 * (x & 7);
            int bits = (int)(corners << shift);
            this.counters[y, x / 8] |= bits;
        }

        for (int x = 0; x < xSize + 1; x++)
        {
            Set(x, 0, TopLeft | TopRight);
            Set(x, ySize, BottomLeft | BottomRight);
        }

        for (int y = 0; y < ySize + 1; y++)
        {
            Set(0, y, TopLeft | BottomLeft);
            Set(xSize, y, TopRight | BottomRight);
        }
    }

    /// <summary>
    /// Clears one group's completion bits before its samples change in another pass.
    /// </summary>
    /// <param name="groupId">The row-major group index.</param>
    public void ClearDone(int groupId)
    {
        void Clear(int x, int y, uint corners)
        {
            int shift = 4 * (x & 7);
            int mask = ~(int)(corners << shift);

            ref int counter = ref this.counters[y, x / 8];
            counter &= mask;
        }

        (int y, int x) = Math.DivRem(groupId, this.frameDim.XSizeGroups);

        Clear(x, y, BottomRight);
        Clear(x + 1, y, BottomLeft);
        Clear(x, y + 1, TopRight);
        Clear(x + 1, y + 1, TopLeft);
    }

    /// <summary>
    /// Marks written group samples complete and returns the regions whose neighbors are also complete.
    /// </summary>
    /// <param name="groupId">The row-major group index. Its completion bits must be clear before this call.</param>
    /// <param name="padx">The horizontal neighbor width required by reconstruction, in pixels.</param>
    /// <param name="pady">The vertical neighbor height required by reconstruction, in pixels.</param>
    /// <param name="rectsToFinalize">Storage for at least <see cref="MaxToFinalize"/> ready rectangles.</param>
    /// <param name="numToFinalize">The number of ready rectangles written.</param>
    public void GroupDone(int groupId, int padx, int pady, Span<Rectangle> rectsToFinalize, out int numToFinalize)
    {
        (int y, int x) = Math.DivRem(groupId, this.frameDim.XSizeGroups);

        Rectangle blockRect = this.frameDim.BlockGroupRect(groupId);

        int FetchStatus(int cx, int cy, uint bit)
        {
            int shift = 4 * (cx & 7);
            int bits = (int)(bit << shift);

            ref int counter = ref this.counters[cy, cx / 8];

            // All group reconstruction uses one decoder thread. Read the previous flags before publishing
            // this group so the final touching group owns the shared corner without an atomic operation.
            int status = counter;
            counter |= bits;
            status >>= shift;

            return (int)((bit | (uint)status) & 0xF);
        }

        int topLeftStatus = FetchStatus(x, y, BottomRight);
        int topRightStatus = FetchStatus(x + 1, y, BottomLeft);
        int bottomRightStatus = FetchStatus(x + 1, y + 1, TopLeft);
        int bottomLeftStatus = FetchStatus(x, y + 1, TopRight);

        int x1 = blockRect.X + blockRect.Width;
        int y1 = blockRect.Y + blockRect.Height;

        bool isLastGroupX = this.frameDim.XSizeGroups == x + 1;
        bool isLastGroupY = this.frameDim.YSizeGroups == y + 1;

        Span<int> xpos =
        [
            blockRect.X == 0
                ? 0
                : (blockRect.X * JxlFrameDimensions.BlockDimensions) - padx,

            blockRect.X == 0
                ? 0
                : Math.Min(
                    this.frameDim.XSize,
                    (blockRect.X * JxlFrameDimensions.BlockDimensions) + padx),

            isLastGroupX
                ? this.frameDim.XSize
                : (x1 * JxlFrameDimensions.BlockDimensions) - padx,

            Math.Min(
                this.frameDim.XSize,
                (x1 * JxlFrameDimensions.BlockDimensions) + padx)
        ];

        Span<int> ypos =
        [
            blockRect.Y == 0
                ? 0
                : (blockRect.Y * JxlFrameDimensions.BlockDimensions) - pady,

            blockRect.Y == 0
                ? 0
                : Math.Min(
                     this.frameDim.YSize,
                     (blockRect.Y * JxlFrameDimensions.BlockDimensions) + pady),

            isLastGroupY
                ? this.frameDim.YSize
                : (y1 * JxlFrameDimensions.BlockDimensions) - pady,

            Math.Min(
                 this.frameDim.YSize,
                 (y1 * JxlFrameDimensions.BlockDimensions) + pady)
        ];

        numToFinalize = 0;

        void AppendRect(int x0, int x1, int y0, int y1, ref int numToFinalize, Span<Rectangle> rectsToFinalize, Span<int> xpos, Span<int> ypos)
        {
            Rectangle rect = new(
                xpos[x0],
                ypos[y0],
                xpos[x1] - xpos[x0],
                ypos[y1] - ypos[y0]);

            if (rect.Width == 0 || rect.Height == 0)
            {
                return;
            }

            rectsToFinalize[numToFinalize++] = rect;
        }

        // Divide the expanded group into three columns and three rows. The center is ready immediately;
        // an edge needs its adjacent group, and a corner needs all four groups. This view owns no heap storage.
        InlineArray16<bool> availableParts = default;
        Span<bool> availablePartsMask = availableParts;

        // Center
        availablePartsMask[4] = true;

        // Corners
        if (topLeftStatus == 0xF)
        {
            availablePartsMask[0] = true;
        }

        if (topRightStatus == 0xF)
        {
            availablePartsMask[2] = true;
        }

        if (bottomRightStatus == 0xF)
        {
            availablePartsMask[8] = true;
        }

        if (bottomLeftStatus == 0xF)
        {
            availablePartsMask[6] = true;
        }

        // Other borders
        if ((topLeftStatus & TopRight) != 0)
        {
            availablePartsMask[1] = true;
        }

        if ((topLeftStatus & BottomLeft) != 0)
        {
            availablePartsMask[3] = true;
        }

        if ((topRightStatus & BottomRight) != 0)
        {
            availablePartsMask[5] = true;
        }

        if ((bottomLeftStatus & BottomRight) != 0)
        {
            availablePartsMask[7] = true;
        }

        const int noSegment = 3;

        Span<(int First, int Last)> horizontalSegments =
        [
            (noSegment, noSegment),
            (noSegment, noSegment),
            (noSegment, noSegment)
        ];

        for (int py = 0; py < 3; py++)
        {
            for (int px = 0; px < 3; px++)
            {
                if (!availablePartsMask[(py * 3) + px])
                {
                    continue;
                }

                if (horizontalSegments[py].First == noSegment)
                {
                    horizontalSegments[py] = (px, horizontalSegments[py].Last);
                }

                horizontalSegments[py] = (horizontalSegments[py].First, px + 1);
            }
        }

        // A ready row cannot have separated left and right parts without its center. Merge equal
        // horizontal intervals in adjacent rows, so each completion needs at most three rectangles.
        if (horizontalSegments[0] == horizontalSegments[1] &&
            horizontalSegments[0] == horizontalSegments[2])
        {
            AppendRect(
                horizontalSegments[0].First,
                horizontalSegments[0].Last,
                0,
                3,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);
        }
        else if (horizontalSegments[0] == horizontalSegments[1])
        {
            AppendRect(
                horizontalSegments[0].First,
                horizontalSegments[0].Last,
                0,
                2,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);

            AppendRect(
                horizontalSegments[2].First,
                horizontalSegments[2].Last,
                2,
                3,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);
        }
        else if (horizontalSegments[1] == horizontalSegments[2])
        {
            AppendRect(
                horizontalSegments[0].First,
                horizontalSegments[0].Last,
                0,
                1,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);

            AppendRect(
                horizontalSegments[1].First,
                horizontalSegments[1].Last,
                1,
                3,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);
        }
        else
        {
            AppendRect(
                horizontalSegments[0].First,
                horizontalSegments[0].Last,
                0,
                1,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);

            AppendRect(
                horizontalSegments[1].First,
                horizontalSegments[1].Last,
                1,
                2,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);

            AppendRect(
                horizontalSegments[2].First,
                horizontalSegments[2].Last,
                2,
                3,
                ref numToFinalize,
                rectsToFinalize,
                xpos,
                ypos);
        }
    }

    /// <summary>
    /// Releases the configured allocator's completion-counter storage.
    /// </summary>
    public void Dispose() => this.counters.Dispose();
}
