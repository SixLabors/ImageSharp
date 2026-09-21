// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Finds corners with a direct transcription of the reference detector.
/// </summary>
/// <remarks>
/// This is the oracle that the corner detector is measured against, so it is deliberately written
/// as the plainest possible form of the reference rather than as fast code. The suppression keeps
/// the two cursors of the reference, so it checks the restructured form of the port rather than
/// repeating it. Reference: fast9_detect(), fast9_corner_score(), aom_nonmax_suppression() and
/// compute_corner_list().
/// </remarks>
internal static class Av1CornerDetectorOracle
{
    /// <summary>
    /// The brightness difference that a circle sample must reach.
    /// </summary>
    private const int Barrier = 18;

    /// <summary>
    /// The number of corners that estimation accepts from one frame.
    /// </summary>
    private const int MaximumCorners = 4096;

    /// <summary>
    /// The samples at each edge that cannot hold the centre of a circle.
    /// </summary>
    private const int Border = 3;

    /// <summary>
    /// Gets the column of each circle sample, in circle order.
    /// </summary>
    private static ReadOnlySpan<int> CircleColumns => [0, 1, 2, 3, 3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -2, -1];

    /// <summary>
    /// Gets the row of each circle sample, in circle order.
    /// </summary>
    private static ReadOnlySpan<int> CircleRows => [3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -2, -1, 0, 1, 2, 3];

    /// <summary>
    /// Finds the corners of one plane.
    /// </summary>
    /// <param name="plane">The whole storage of the plane.</param>
    /// <param name="origin">The index of the first coded sample of the plane.</param>
    /// <param name="width">The width of the plane.</param>
    /// <param name="height">The height of the plane.</param>
    /// <param name="stride">The row stride of the plane.</param>
    /// <returns>The column and row of each corner, interleaved.</returns>
    public static int[] Detect(ReadOnlySpan<byte> plane, int origin, int width, int height, int stride)
    {
        List<int> columns = [];
        List<int> rows = [];
        for (int y = Border; y < height - Border; y++)
        {
            for (int x = Border; x < width - Border; x++)
            {
                if (IsCorner(plane, origin, stride, x, y, Barrier))
                {
                    columns.Add(x);
                    rows.Add(y);
                }
            }
        }

        int total = columns.Count;
        if (total == 0)
        {
            return [];
        }

        int[] scores = new int[total];
        for (int i = 0; i < total; i++)
        {
            scores[i] = Score(plane, origin, stride, columns[i], rows[i]);
        }

        return Cap(Suppress(columns, rows, scores));
    }

    /// <summary>
    /// Tests whether one pixel is a corner at one barrier.
    /// </summary>
    /// <param name="plane">The whole storage of the plane.</param>
    /// <param name="origin">The index of the first coded sample of the plane.</param>
    /// <param name="stride">The row stride of the plane.</param>
    /// <param name="x">The column of the pixel.</param>
    /// <param name="y">The row of the pixel.</param>
    /// <param name="barrier">The barrier.</param>
    /// <returns>Whether nine adjacent circle samples are all brighter or all darker.</returns>
    private static bool IsCorner(ReadOnlySpan<byte> plane, int origin, int stride, int x, int y, int barrier)
    {
        int centre = plane[origin + (y * stride) + x];
        bool[] brighter = new bool[16];
        bool[] darker = new bool[16];
        for (int point = 0; point < 16; point++)
        {
            int sample = plane[origin + ((y + CircleRows[point]) * stride) + x + CircleColumns[point]];
            brighter[point] = sample > centre + barrier;
            darker[point] = sample < centre - barrier;
        }

        return HasRun(brighter) || HasRun(darker);
    }

    /// <summary>
    /// Tests whether nine adjacent entries of a circular table are all set.
    /// </summary>
    /// <param name="values">The sixteen entries.</param>
    /// <returns>Whether a run of nine exists.</returns>
    private static bool HasRun(bool[] values)
    {
        for (int start = 0; start < 16; start++)
        {
            int run = 0;
            while (run < 9 && values[(start + run) % 16])
            {
                run++;
            }

            if (run == 9)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Scores one corner by the largest barrier at which it is still a corner.
    /// </summary>
    /// <param name="plane">The whole storage of the plane.</param>
    /// <param name="origin">The index of the first coded sample of the plane.</param>
    /// <param name="stride">The row stride of the plane.</param>
    /// <param name="x">The column of the corner.</param>
    /// <param name="y">The row of the corner.</param>
    /// <returns>The score.</returns>
    private static int Score(ReadOnlySpan<byte> plane, int origin, int stride, int x, int y)
    {
        int lower = Barrier;
        int upper = 255;
        while (upper - lower > 1)
        {
            int barrier = (lower + upper) / 2;
            if (IsCorner(plane, origin, stride, x, y, barrier))
            {
                lower = barrier;
            }
            else
            {
                upper = barrier;
            }
        }

        return lower;
    }

    /// <summary>
    /// Keeps the corners that no neighboring corner beats.
    /// </summary>
    /// <param name="columns">The corner columns, in raster order.</param>
    /// <param name="rows">The corner rows, in raster order.</param>
    /// <param name="scores">The corner scores.</param>
    /// <returns>The column, row and score of each survivor.</returns>
    private static List<int[]> Suppress(List<int> columns, List<int> rows, int[] scores)
    {
        int size = columns.Count;
        int lastRow = rows[size - 1];
        int[] rowStart = new int[lastRow + 1];
        Array.Fill(rowStart, -1);

        int previousRow = -1;
        for (int i = 0; i < size; i++)
        {
            if (rows[i] != previousRow)
            {
                rowStart[rows[i]] = i;
                previousRow = rows[i];
            }
        }

        List<int[]> survivors = [];
        int pointAbove = 0;
        int pointBelow = 0;
        for (int i = 0; i < size; i++)
        {
            int score = scores[i];
            int x = columns[i];
            int y = rows[i];

            if (i > 0 && columns[i - 1] == x - 1 && rows[i - 1] == y && scores[i - 1] >= score)
            {
                continue;
            }

            if (i < size - 1 && columns[i + 1] == x + 1 && rows[i + 1] == y && scores[i + 1] >= score)
            {
                continue;
            }

            bool beaten = false;
            if (y > 0 && rowStart[y - 1] != -1)
            {
                if (rows[pointAbove] < y - 1)
                {
                    pointAbove = rowStart[y - 1];
                }

                while (rows[pointAbove] < y && columns[pointAbove] < x - 1)
                {
                    pointAbove++;
                }

                for (int j = pointAbove; rows[j] < y && columns[j] <= x + 1; j++)
                {
                    if (columns[j] >= x - 1 && scores[j] >= score)
                    {
                        beaten = true;
                        break;
                    }
                }
            }

            if (!beaten && y + 1 < lastRow + 1 && rowStart[y + 1] != -1 && pointBelow < size)
            {
                if (rows[pointBelow] < y + 1)
                {
                    pointBelow = rowStart[y + 1];
                }

                while (pointBelow < size && rows[pointBelow] == y + 1 && columns[pointBelow] < x - 1)
                {
                    pointBelow++;
                }

                for (int j = pointBelow; j < size && rows[j] == y + 1 && columns[j] <= x + 1; j++)
                {
                    if (columns[j] >= x - 1 && scores[j] >= score)
                    {
                        beaten = true;
                        break;
                    }
                }
            }

            if (!beaten)
            {
                survivors.Add([x, y, score]);
            }
        }

        return survivors;
    }

    /// <summary>
    /// Keeps only the sharpest survivors when there are more than the limit.
    /// </summary>
    /// <param name="survivors">The column, row and score of each survivor.</param>
    /// <returns>The column and row of each corner, interleaved.</returns>
    private static int[] Cap(List<int[]> survivors)
    {
        List<int> corners = [];
        if (survivors.Count <= MaximumCorners)
        {
            foreach (int[] survivor in survivors)
            {
                corners.Add(survivor[0]);
                corners.Add(survivor[1]);
            }

            return [.. corners];
        }

        int[] histogram = new int[256];
        foreach (int[] survivor in survivors)
        {
            histogram[survivor[2]]++;
        }

        int threshold = 0;
        int found = 0;
        for (int bucket = 255; bucket >= 0; bucket--)
        {
            if (found + histogram[bucket] > MaximumCorners)
            {
                threshold = bucket;
                break;
            }

            found += histogram[bucket];
        }

        foreach (int[] survivor in survivors)
        {
            if (survivor[2] > threshold)
            {
                corners.Add(survivor[0]);
                corners.Add(survivor[1]);
            }
        }

        return [.. corners];
    }
}
