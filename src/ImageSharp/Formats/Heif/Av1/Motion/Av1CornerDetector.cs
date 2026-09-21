// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Finds the corners that global motion estimation tracks between two frames.
/// </summary>
/// <remarks>
/// <para>
/// The detector is FAST with a circle of sixteen samples and a run of nine. A pixel is a corner when
/// nine adjacent samples of the circle around it are all brighter than it by the barrier, or all
/// darker than it by the barrier.
/// </para>
/// <para>
/// The reference implements the run test as a generated decision tree over one pixel at a time. The
/// tree is a compression of the rule stated above and accepts the same pixels, so this port states
/// the rule and tests a whole row of pixels at once. A decision tree cannot be vectorized, because
/// adjacent pixels take different branches, whereas the rule is a fixed sequence of comparisons that
/// every pixel shares.
/// </para>
/// <para>
/// Reference: aom_fast9_detect_nonmax(), fast9_detect(), fast9_corner_score(),
/// aom_nonmax_suppression() and compute_corner_list().
/// </para>
/// </remarks>
internal static partial class Av1CornerDetector
{
    /// <summary>
    /// The number of samples on the circle around one pixel.
    /// </summary>
    private const int CircleLength = 16;

    /// <summary>
    /// The number of adjacent circle samples that make a corner.
    /// </summary>
    private const int RunLength = 9;

    /// <summary>
    /// The brightness difference that a circle sample must reach.
    /// </summary>
    /// <remarks>Reference: FAST_BARRIER.</remarks>
    public const int Barrier = 18;

    /// <summary>
    /// The number of corners that estimation accepts from one frame.
    /// </summary>
    /// <remarks>Reference: MAX_CORNERS.</remarks>
    public const int MaximumCorners = 4096;

    /// <summary>
    /// The samples at each edge of the plane that cannot hold the centre of a circle.
    /// </summary>
    private const int Border = 3;

    /// <summary>
    /// The largest barrier that the corner score search considers.
    /// </summary>
    private const int MaximumBarrier = 255;

    /// <summary>
    /// The number of candidates that the first allocation of the candidate list holds.
    /// </summary>
    /// <remarks>
    /// The list grows by doubling, as the reference does, because the number of corners in a frame
    /// cannot be predicted and the worst case of one corner per pixel is far larger than any real
    /// frame produces.
    /// </remarks>
    private const int InitialCapacity = 1024;

    /// <summary>
    /// Gets the column of each circle sample, in circle order.
    /// </summary>
    /// <remarks>Reference: make_offsets().</remarks>
    private static ReadOnlySpan<sbyte> CircleColumns => [0, 1, 2, 3, 3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -2, -1];

    /// <summary>
    /// Gets the row of each circle sample, in circle order.
    /// </summary>
    /// <remarks>Reference: make_offsets().</remarks>
    private static ReadOnlySpan<sbyte> CircleRows => [3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -2, -1, 0, 1, 2, 3];

    /// <summary>
    /// Finds the corners of one eight-bit plane.
    /// </summary>
    /// <param name="allocator">The allocator of the candidate lists.</param>
    /// <param name="plane">The whole storage of the plane.</param>
    /// <param name="origin">The index of the first coded sample of the plane.</param>
    /// <param name="width">The width of the plane.</param>
    /// <param name="height">The height of the plane.</param>
    /// <param name="stride">The row stride of the plane.</param>
    /// <param name="corners">
    /// Receives the column and row of each corner, interleaved. It must hold twice
    /// <see cref="MaximumCorners"/> values.
    /// </param>
    /// <returns>The number of corners written.</returns>
    /// <remarks>
    /// The detector never reads outside the coded plane, because a circle of radius three needs
    /// three samples of context and the traversal starts three samples inside each edge.
    /// </remarks>
    public static int Detect(
        MemoryAllocator allocator,
        ReadOnlySpan<byte> plane,
        int origin,
        int width,
        int height,
        int stride,
        Span<int> corners)
    {
        int count = width - (2 * Border);
        if (count <= 0 || height <= 2 * Border)
        {
            return 0;
        }

        // A circle sample is one offset from the centre, so the two coordinate tables collapse into
        // one table of sample offsets as soon as the stride is known.
        Span<int> offsets = stackalloc int[CircleLength];
        for (int point = 0; point < CircleLength; point++)
        {
            offsets[point] = CircleColumns[point] + (CircleRows[point] * stride);
        }

        using Candidates candidates = new(allocator);
        using IMemoryOwner<byte> markOwner = allocator.Allocate<byte>(count);
        Span<byte> marks = markOwner.Memory.Span;
        ref byte marksBase = ref MemoryMarshal.GetReference(marks);
        ref byte planeBase = ref MemoryMarshal.GetReference(plane);

        for (int row = Border; row < height - Border; row++)
        {
            // Both directions of the threshold set marks in one row, so the row is cleared once and
            // each direction then contributes to it.
            marks.Clear();
            ref byte centre = ref Unsafe.Add(ref planeBase, origin + (row * stride) + Border);
            Runs<BrighterOperator>.Mark(ref centre, offsets, Barrier, ref marksBase, count);
            Runs<DarkerOperator>.Mark(ref centre, offsets, Barrier, ref marksBase, count);

            // Suppression relies on the candidates arriving in raster order, which is the order
            // this scan produces.
            for (int index = 0; index < count; index++)
            {
                if (marks[index] != 0)
                {
                    candidates.Add(Border + index, row);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        ScoreCandidates(candidates, ref planeBase, origin, stride, offsets);
        return Suppress(allocator, candidates, corners);
    }

    /// <summary>
    /// Scores every candidate by the largest barrier at which it is still a corner.
    /// </summary>
    /// <param name="candidates">The candidates, in raster order.</param>
    /// <param name="planeBase">The first sample of the storage of the plane.</param>
    /// <param name="origin">The index of the first coded sample of the plane.</param>
    /// <param name="stride">The row stride of the plane.</param>
    /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
    /// <remarks>
    /// The score is found by bisection between the detection barrier, at which a candidate is a
    /// corner by construction, and the largest barrier, at which nothing is a corner. Suppression
    /// compares these scores, so a sharper corner must score higher.
    /// Reference: fast9_corner_score().
    /// </remarks>
    private static void ScoreCandidates(
        Candidates candidates,
        ref byte planeBase,
        int origin,
        int stride,
        ReadOnlySpan<int> offsets)
    {
        Span<int> positions = candidates.Positions;
        Span<int> scores = candidates.Scores;
        for (int index = 0; index < candidates.Count; index++)
        {
            ref byte centre = ref Unsafe.Add(
                ref planeBase, origin + (positions[(2 * index) + 1] * stride) + positions[2 * index]);

            int lower = Barrier;
            int upper = MaximumBarrier;
            while (upper - lower > 1)
            {
                int barrier = (lower + upper) / 2;
                if (IsCorner(ref centre, offsets, barrier))
                {
                    lower = barrier;
                }
                else
                {
                    upper = barrier;
                }
            }

            scores[index] = lower;
        }
    }

    /// <summary>
    /// Tests whether one pixel is a corner at one barrier.
    /// </summary>
    /// <param name="centre">The centre sample.</param>
    /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
    /// <param name="barrier">The barrier.</param>
    /// <returns>Whether nine adjacent circle samples are all brighter or all darker.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCorner(ref byte centre, ReadOnlySpan<int> offsets, int barrier)
        => Runs<BrighterOperator>.Test(ref centre, offsets, barrier) ||
           Runs<DarkerOperator>.Test(ref centre, offsets, barrier);

    /// <summary>
    /// Keeps the candidates that no neighboring candidate beats, then caps the survivors.
    /// </summary>
    /// <param name="allocator">The allocator of the row index and the survivor list.</param>
    /// <param name="candidates">The scored candidates, in raster order.</param>
    /// <param name="corners">Receives the column and row of each corner, interleaved.</param>
    /// <returns>The number of corners written.</returns>
    /// <remarks>
    /// A candidate is dropped when any of the eight candidates adjacent to it scores at least as
    /// high. The comparison is not strict, so a plateau of equal scores collapses to nothing rather
    /// than to every member of the plateau, which is what the reference does.
    /// Reference: aom_nonmax_suppression().
    /// </remarks>
    private static int Suppress(MemoryAllocator allocator, Candidates candidates, Span<int> corners)
    {
        ReadOnlySpan<int> positions = candidates.Positions;
        ReadOnlySpan<int> scores = candidates.Scores;
        int total = candidates.Count;
        int lastRow = positions[(2 * (total - 1)) + 1];

        // The candidates are in raster order, so the first candidate of a row indexes that row. A
        // row with no candidate keeps the empty marker.
        using IMemoryOwner<int> rowStartOwner = allocator.Allocate<int>(lastRow + 1);
        Span<int> rowStart = rowStartOwner.Memory.Span[..(lastRow + 1)];
        rowStart.Fill(-1);

        int previousRow = -1;
        for (int index = 0; index < total; index++)
        {
            int row = positions[(2 * index) + 1];
            if (row != previousRow)
            {
                rowStart[row] = index;
                previousRow = row;
            }
        }

        // The survivors carry their score into the cap below, so each one occupies three values.
        using IMemoryOwner<int> survivorOwner = allocator.Allocate<int>(3 * total);
        Span<int> survivors = survivorOwner.Memory.Span;
        int surviving = 0;

        for (int index = 0; index < total; index++)
        {
            int column = positions[2 * index];
            int row = positions[(2 * index) + 1];
            int score = scores[index];

            // The candidate to the left and the candidate to the right are adjacent in raster
            // order, so neither needs a search.
            if (index > 0 &&
                positions[2 * (index - 1)] == column - 1 &&
                positions[(2 * (index - 1)) + 1] == row &&
                scores[index - 1] >= score)
            {
                continue;
            }

            if (index < total - 1 &&
                positions[2 * (index + 1)] == column + 1 &&
                positions[(2 * (index + 1)) + 1] == row &&
                scores[index + 1] >= score)
            {
                continue;
            }

            if (IsBeatenByRow(positions, scores, rowStart, total, row - 1, column, score) ||
                IsBeatenByRow(positions, scores, rowStart, total, row + 1, column, score))
            {
                continue;
            }

            survivors[3 * surviving] = column;
            survivors[(3 * surviving) + 1] = row;
            survivors[(3 * surviving) + 2] = score;
            surviving++;
        }

        return Cap(survivors, surviving, corners);
    }

    /// <summary>
    /// Tests whether any candidate of one neighboring row beats one candidate.
    /// </summary>
    /// <param name="positions">The candidate columns and rows, interleaved.</param>
    /// <param name="scores">The candidate scores.</param>
    /// <param name="rowStart">The first candidate of each row, or minus one for an empty row.</param>
    /// <param name="total">The number of candidates.</param>
    /// <param name="row">The neighboring row.</param>
    /// <param name="column">The column of the candidate under test.</param>
    /// <param name="score">The score of the candidate under test.</param>
    /// <returns>Whether a candidate of that row is adjacent and scores at least as high.</returns>
    /// <remarks>
    /// The reference carries one cursor per direction across the whole scan, which relies on the
    /// candidates and the queries both advancing in raster order. This walks from the start of the
    /// neighboring row instead. It reaches the same answer from the row index alone and keeps no
    /// state between candidates, and the walk stops at the first column past the neighborhood, so
    /// it reads at most a few entries.
    /// </remarks>
    private static bool IsBeatenByRow(
        ReadOnlySpan<int> positions,
        ReadOnlySpan<int> scores,
        ReadOnlySpan<int> rowStart,
        int total,
        int row,
        int column,
        int score)
    {
        if (row < 0 || row >= rowStart.Length || rowStart[row] == -1)
        {
            return false;
        }

        for (int index = rowStart[row]; index < total && positions[(2 * index) + 1] == row; index++)
        {
            int neighbor = positions[2 * index];
            if (neighbor > column + 1)
            {
                return false;
            }

            if (neighbor >= column - 1 && scores[index] >= score)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the corners, keeping only the sharpest when there are more than the limit.
    /// </summary>
    /// <param name="survivors">The column, row and score of each survivor.</param>
    /// <param name="surviving">The number of survivors.</param>
    /// <param name="corners">Receives the column and row of each corner, interleaved.</param>
    /// <returns>The number of corners written.</returns>
    /// <remarks>
    /// Estimation accepts a fixed number of corners and the sharpest are the most useful, so the
    /// survivors are capped by score rather than by position. A histogram of the scores gives the
    /// threshold in one pass, because a score is a barrier and so already lies between the
    /// detection barrier and 255. The threshold is the first bucket, descending, whose whole
    /// contents would pass the limit, and only scores above it are kept. The result is therefore at
    /// most the limit and never splits a bucket. Reference: compute_corner_list().
    /// </remarks>
    private static int Cap(ReadOnlySpan<int> survivors, int surviving, Span<int> corners)
    {
        if (surviving <= MaximumCorners)
        {
            for (int index = 0; index < surviving; index++)
            {
                corners[2 * index] = survivors[3 * index];
                corners[(2 * index) + 1] = survivors[(3 * index) + 1];
            }

            return surviving;
        }

        Span<int> histogram = stackalloc int[MaximumBarrier + 1];
        histogram.Clear();
        for (int index = 0; index < surviving; index++)
        {
            histogram[survivors[(3 * index) + 2]]++;
        }

        int threshold = 0;
        int accepted = 0;
        for (int bucket = MaximumBarrier; bucket >= 0; bucket--)
        {
            if (accepted + histogram[bucket] > MaximumCorners)
            {
                threshold = bucket;
                break;
            }

            accepted += histogram[bucket];
        }

        int written = 0;
        for (int index = 0; index < surviving; index++)
        {
            if (survivors[(3 * index) + 2] > threshold)
            {
                corners[2 * written] = survivors[3 * index];
                corners[(2 * written) + 1] = survivors[(3 * index) + 1];
                written++;
            }
        }

        return written;
    }

    /// <summary>
    /// Holds the candidates of one plane, growing as the scan finds them.
    /// </summary>
    /// <remarks>
    /// The number of candidates depends on the content, so no fixed bound is correct: a small bound
    /// would drop corners, and the true bound of one candidate per pixel would reserve far more
    /// than any real frame needs. The list therefore doubles, and both buffers come from the
    /// allocator rather than from the heap, so repeated frames reuse the same memory.
    /// </remarks>
    private sealed class Candidates : IDisposable
    {
        /// <summary>
        /// The allocator of both buffers.
        /// </summary>
        private readonly MemoryAllocator allocator;

        /// <summary>
        /// The column and row of each candidate, interleaved.
        /// </summary>
        private IMemoryOwner<int> positions;

        /// <summary>
        /// The score of each candidate, filled after the scan ends.
        /// </summary>
        private IMemoryOwner<int> scores;

        /// <summary>
        /// The number of candidates that the buffers hold.
        /// </summary>
        private int capacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="Candidates"/> class.
        /// </summary>
        /// <param name="allocator">The allocator of both buffers.</param>
        public Candidates(MemoryAllocator allocator)
        {
            this.allocator = allocator;
            this.capacity = InitialCapacity;
            this.positions = allocator.Allocate<int>(2 * InitialCapacity);
            this.scores = allocator.Allocate<int>(InitialCapacity);
        }

        /// <summary>
        /// Gets the number of candidates.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gets the column and row of each candidate, interleaved.
        /// </summary>
        public Span<int> Positions => this.positions.Memory.Span;

        /// <summary>
        /// Gets the score of each candidate.
        /// </summary>
        public Span<int> Scores => this.scores.Memory.Span;

        /// <summary>
        /// Appends one candidate.
        /// </summary>
        /// <param name="column">The column of the candidate.</param>
        /// <param name="row">The row of the candidate.</param>
        public void Add(int column, int row)
        {
            if (this.Count == this.capacity)
            {
                this.Grow();
            }

            Span<int> target = this.Positions;
            target[2 * this.Count] = column;
            target[(2 * this.Count) + 1] = row;
            this.Count++;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.positions.Dispose();
            this.scores.Dispose();
        }

        /// <summary>
        /// Doubles both buffers, keeping the candidates already found.
        /// </summary>
        /// <remarks>
        /// The score buffer holds nothing until the scan ends, so only the positions are copied.
        /// </remarks>
        private void Grow()
        {
            int grown = this.capacity * 2;
            IMemoryOwner<int> positionsGrown = this.allocator.Allocate<int>(2 * grown);
            this.positions.Memory.Span[..(2 * this.Count)].CopyTo(positionsGrown.Memory.Span);
            this.positions.Dispose();
            this.positions = positionsGrown;

            this.scores.Dispose();
            this.scores = this.allocator.Allocate<int>(grown);
            this.capacity = grown;
        }
    }
}
