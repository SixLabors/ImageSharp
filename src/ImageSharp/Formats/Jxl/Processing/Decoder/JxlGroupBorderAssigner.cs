// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlGroupBorderAssigner
{
    public const int MaxToFinalize = 3;

    private readonly JxlFrameDimensions frameDim;
    private readonly Buffer2D<int> counters;

    private const byte TopLeft = 0x01;
    private const byte TopRight = 0x02;
    private const byte BottomRight = 0x04;
    private const byte BottomLeft = 0x08;

    public JxlGroupBorderAssigner(Configuration configuration, JxlFrameDimensions frameDim)
    {
        this.frameDim = frameDim;

        int xSize = frameDim.XSizeGroups;
        int ySize = frameDim.YSizeGroups;

        int counterCount = (xSize + 1 + 7) / 8;
        this.counters = configuration.MemoryAllocator.Allocate2D<int>(ySize + 1, counterCount);

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

    public void ClearDone(int groupId)
    {
        void Clear(int x, int y, uint corners)
        {
            int shift = 4 * (x & 7);
            int mask = ~(int)(corners << shift);

            ref int counter = ref this.counters[y, x / 8];
            _ = Interlocked.And(ref counter, mask);
        }

        (int x, int y) = Math.DivRem(groupId, this.frameDim.XSizeGroups);

        Clear(x, y, BottomRight);
        Clear(x + 1, y, BottomLeft);
        Clear(x, y + 1, TopRight);
        Clear(x + 1, y + 1, TopLeft);
    }

    public void GroupDone(int groupId, int padx, int pady, Span<Rectangle> rectsToFinalize, out int numToFinalize)
    {
        (int x, int y) = Math.DivRem(groupId, this.frameDim.XSizeGroups);

        Rectangle blockRect = RectangleUtils.CreateRectangle(
            x * this.frameDim.GroupDimension / JxlFrameDimensions.BlockDimensions,
            y * this.frameDim.GroupDimension / JxlFrameDimensions.BlockDimensions,
            this.frameDim.GroupDimension / JxlFrameDimensions.BlockDimensions,
            this.frameDim.GroupDimension / JxlFrameDimensions.BlockDimensions,
            this.frameDim.XSizeBlocks,
            this.frameDim.YSizeBlocks);

        int FetchStatus(int cx, int cy, uint bit)
        {
            int shift = 4 * (cx & 7);
            int bits = (int)(bit << shift);

            ref int counter = ref this.counters[cy, cx / 8];

            int status = Interlocked.Or(ref counter, bits);
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

        bool[,] availablePartsMask = new bool[3, 3];

        // Center
        availablePartsMask[1, 1] = true;

        // Corners
        if (topLeftStatus == 0xF)
        {
            availablePartsMask[0, 0] = true;
        }

        if (topRightStatus == 0xF)
        {
            availablePartsMask[2, 0] = true;
        }

        if (bottomRightStatus == 0xF)
        {
            availablePartsMask[2, 2] = true;
        }

        if (bottomLeftStatus == 0xF)
        {
            availablePartsMask[0, 2] = true;
        }

        // Other borders
        if ((topLeftStatus & TopRight) != 0)
        {
            availablePartsMask[1, 0] = true;
        }

        if ((topLeftStatus & BottomLeft) != 0)
        {
            availablePartsMask[0, 1] = true;
        }

        if ((topRightStatus & BottomRight) != 0)
        {
            availablePartsMask[2, 1] = true;
        }

        if ((bottomLeftStatus & BottomRight) != 0)
        {
            availablePartsMask[1, 2] = true;
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
                if (!availablePartsMask[px, py])
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
}
