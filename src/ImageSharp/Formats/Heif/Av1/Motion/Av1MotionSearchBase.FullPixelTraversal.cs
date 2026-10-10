// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Holds the full-sample diamond and mesh searches. The coding-pass search, the first pass and the OBMC search share them.
/// Only the error measure differs, so each caller supplies it as a struct cost type. The JIT compiles one copy of each search per cost type,
/// so the cost calls are direct and can inline.
/// </content>
internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Measures full-sample candidates for <see cref="SearchDiamond{TCost}"/> and <see cref="SearchMesh{TCost}"/>.
    /// </summary>
    /// <remarks>
    /// The searches rank candidates by the absolute difference plus its motion-rate cost. They compare finished search paths by the variance
    /// plus its motion-rate cost. The absolute difference must not be negative and the rate cost must not be negative, because the searches
    /// skip the rate lookup when the absolute difference alone cannot win.
    /// </remarks>
    public interface IFullPixelCost
    {
        /// <summary>
        /// Gets the reference index of a candidate. A search adds a site offset to the index of its center, so the index must grow by the
        /// reference stride for each row.
        /// </summary>
        /// <param name="vector">The candidate displacement in full samples.</param>
        /// <returns>The reference index of the displaced block origin.</returns>
        public int GetReferenceIndex(Point vector);

        /// <summary>
        /// Measures the absolute difference of the candidate at a reference index.
        /// </summary>
        /// <param name="referenceIndex">The reference index of the displaced block origin.</param>
        /// <returns>The absolute-difference error in the eight-bit error domain.</returns>
        public int GetSad(int referenceIndex);

        /// <summary>
        /// Gets the motion-rate cost of a candidate in the absolute-difference domain.
        /// </summary>
        /// <param name="vector">The candidate displacement in full samples.</param>
        /// <returns>The rate cost. It is never negative.</returns>
        public int GetSadRateCost(Point vector);

        /// <summary>
        /// Measures the variance and the variance-domain motion-rate cost of a finished search path.
        /// </summary>
        /// <param name="vector">The winner of the path in full samples.</param>
        /// <returns>The variance, squared error and motion cost of the winner.</returns>
        public FullPixelResult GetVarianceResult(Point vector);
    }

    /// <summary>
    /// Runs diamond searches of decreasing initial radius from the same start and keeps the winner with the lowest variance cost.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="start">The initial displacement in full samples. It must lie inside <paramref name="bounds"/>.</param>
    /// <param name="stepParameter">The number of outer search stages to exclude from the first path.</param>
    /// <param name="sites">The site geometry, configured for the reference stride of <paramref name="cost"/>.</param>
    /// <param name="bounds">The permitted displacements, with exclusive upper edges.</param>
    /// <param name="skipRepeatedRadii">
    /// Whether a stage that stays at its center skips the next stages of the same radius. The OBMC search does not skip them.
    /// </param>
    /// <param name="secondBest">The preceding winner, updated at each move.</param>
    /// <returns>The winner with the lowest variance cost. On equal cost, the earlier path wins.</returns>
    public static FullPixelResult SearchDiamond<TCost>(
        ref TCost cost,
        Point start,
        int stepParameter,
        Av1MotionSearchSites sites,
        Rectangle bounds,
        bool skipRepeatedRadii,
        ref Point? secondBest)
        where TCost : struct, IFullPixelCost, allows ref struct
    {
        int startCost = GetSadCost(ref cost, start);
        Point winner = SearchDiamondSteps(ref cost, start, startCost, stepParameter, sites, bounds, skipRepeatedRadii, ref secondBest, out int centeredSteps);
        FullPixelResult best = cost.GetVarianceResult(winner);
        int furtherSteps = sites.StageCount - 1 - stepParameter;

        // Each restart searches from the start again with fewer outer stages.
        // The stages that the earlier paths spent at the start, plus one, set how many more outer stages the restart excludes.
        // A path that stays at the start for some stages already did the work of the restarts that exclude those stages, so the count skips them.
        while (centeredSteps < furtherSteps)
        {
            centeredSteps++;
            winner = SearchDiamondSteps(
                ref cost, start, startCost, stepParameter + centeredSteps, sites, bounds, skipRepeatedRadii, ref secondBest, out int skippedSteps);

            FullPixelResult candidate = cost.GetVarianceResult(winner);
            if (candidate.Cost < best.Cost)
            {
                best = candidate;
            }

            centeredSteps += skippedSteps;
        }

        return best;
    }

    /// <summary>
    /// Runs content-selected mesh passes, and adjusts the initial range to the current displacement magnitude.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="start">The winner of the stepped search in full samples.</param>
    /// <param name="pattern">Four range and interval pairs, in full samples.</param>
    /// <param name="fineInterval">Whether the initial interval is capped at four.</param>
    /// <param name="bounds">The permitted displacements, with exclusive upper edges.</param>
    /// <param name="secondBest">The preceding winner, updated at each improvement.</param>
    /// <returns>The mesh winner with its variance cost.</returns>
    public static FullPixelResult SearchMesh<TCost>(
        ref TCost cost,
        Point start,
        ReadOnlySpan<int> pattern,
        bool fineInterval,
        Rectangle bounds,
        ref Point? secondBest)
        where TCost : struct, IFullPixelCost, allows ref struct
    {
        // The first range grows to 5/4 of the largest start component, up to 256 samples.
        // The interval grows by the same ratio, so the first pass keeps its number of steps.
        int originalRange = pattern[0];
        int interval = pattern[1];
        int range = Math.Min(Math.Max(originalRange, (5 * Math.Max(Math.Abs(start.X), Math.Abs(start.Y))) / 4), 256);
        interval = Math.Max(interval, range / (originalRange / interval));
        if (fineInterval)
        {
            interval = Math.Min(interval, 4);
        }

        // A coarse first pass continues with the finer passes of the pattern. It stops after the first pass with a unit interval.
        Point best = SearchMeshPass(ref cost, start, range, interval, bounds, ref secondBest);
        if (interval > 1 && range > 7)
        {
            for (int pass = 1; pass < 4; pass++)
            {
                best = SearchMeshPass(ref cost, best, pattern[pass * 2], pattern[(pass * 2) + 1], bounds, ref secondBest);
                if (pattern[(pass * 2) + 1] == 1)
                {
                    break;
                }
            }
        }

        return cost.GetVarianceResult(best);
    }

    /// <summary>
    /// Measures a candidate and lowers the best cost when the candidate costs less.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="vector">The candidate displacement in full samples.</param>
    /// <param name="referenceIndex">The reference index of the candidate.</param>
    /// <param name="bestCost">The best cost so far, replaced when the candidate costs less.</param>
    /// <returns><see langword="true"/> when the candidate has a strictly lower cost.</returns>
    public static bool TryImproveSad<TCost>(ref TCost cost, Point vector, int referenceIndex, ref int bestCost)
        where TCost : struct, IFullPixelCost, allows ref struct
    {
        // The rate term is never negative, so an absolute difference that already reaches the best cost cannot win. That test skips the rate lookup.
        int sad = cost.GetSad(referenceIndex);
        if (sad >= bestCost)
        {
            return false;
        }

        int total = sad + cost.GetSadRateCost(vector);
        if (total >= bestCost)
        {
            return false;
        }

        bestCost = total;
        return true;
    }

    /// <summary>
    /// Measures the absolute difference plus the motion-rate cost of a candidate.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="vector">The candidate displacement in full samples.</param>
    /// <returns>The absolute-difference cost.</returns>
    public static int GetSadCost<TCost>(ref TCost cost, Point vector)
        where TCost : struct, IFullPixelCost, allows ref struct
        => cost.GetSad(cost.GetReferenceIndex(vector)) + cost.GetSadRateCost(vector);

    /// <summary>
    /// Clamps a displacement to the permitted range.
    /// </summary>
    /// <param name="vector">The displacement in full samples.</param>
    /// <param name="bounds">The permitted displacements, with exclusive upper edges.</param>
    /// <returns>The clamped displacement.</returns>
    public static Point ClampToBounds(Point vector, Rectangle bounds)
        => new(Math.Clamp(vector.X, bounds.Left, bounds.Right - 1), Math.Clamp(vector.Y, bounds.Top, bounds.Bottom - 1));

    /// <summary>
    /// Visits ordered sites once per radius, from the outermost searched radius inward, and counts the initial center stays for later restart
    /// pruning.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="start">The initial displacement in full samples.</param>
    /// <param name="startCost">The absolute-difference cost of the start.</param>
    /// <param name="stepParameter">The number of outer search stages to exclude.</param>
    /// <param name="sites">The site geometry, configured for the reference stride of <paramref name="cost"/>.</param>
    /// <param name="bounds">The permitted displacements, with exclusive upper edges.</param>
    /// <param name="skipRepeatedRadii">Whether a stage that stays at its center skips the next stages of the same radius.</param>
    /// <param name="secondBest">The preceding winner, updated at each move.</param>
    /// <param name="centeredSteps">The number of stages before the first move from the start, plus the repeated radii that the search skipped.</param>
    /// <returns>The winning displacement in full samples.</returns>
    private static Point SearchDiamondSteps<TCost>(
        ref TCost cost,
        Point start,
        int startCost,
        int stepParameter,
        Av1MotionSearchSites sites,
        Rectangle bounds,
        bool skipRepeatedRadii,
        ref Point? secondBest,
        out int centeredSteps)
        where TCost : struct, IFullPixelCost, allows ref struct
    {
        Point best = start;
        int bestCost = startCost;
        bool movedFromStart = false;
        centeredSteps = 0;
        for (int stage = sites.StageCount - stepParameter - 1; stage >= 0; stage--)
        {
            // Site zero is the center. Each stage tests the other sites around the current winner, and moves to the cheapest one after the stage.
            // On equal cost, the earlier site stays.
            ReadOnlySpan<Av1MotionSearchSites.Site> stageSites = sites.GetSites(stage);
            int centerIndex = cost.GetReferenceIndex(best);
            int bestSite = 0;
            for (int index = 1; index <= sites.GetCandidateCount(stage); index++)
            {
                Av1MotionSearchSites.Site site = stageSites[index];
                Point candidate = new(best.X + site.Column, best.Y + site.Row);
                if (bounds.Contains(candidate) && TryImproveSad(ref cost, candidate, centerIndex + site.Offset, ref bestCost))
                {
                    bestSite = index;
                }
            }

            if (bestSite != 0)
            {
                secondBest = best;
                Av1MotionSearchSites.Site site = stageSites[bestSite];
                best = new Point(best.X + site.Column, best.Y + site.Row);
                movedFromStart = true;
            }

            if (!movedFromStart)
            {
                centeredSteps++;
            }

            // After a center stay, the search can skip repeated outer radii. After a move, these radii stay eligible.
            if (skipRepeatedRadii && bestSite == 0 && stage > 2)
            {
                while (stage > 2 && sites.GetRadius(stage - 1) == sites.GetRadius(stage))
                {
                    centeredSteps++;
                    stage--;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Scans mesh rows from a fixed center. Each strict replacement keeps the previous winner as the second best.
    /// </summary>
    /// <typeparam name="TCost">The struct type that measures the error.</typeparam>
    /// <param name="cost">The error measure of the caller.</param>
    /// <param name="start">The pass center in full samples. The pass clamps it to the bounds.</param>
    /// <param name="range">The largest distance from the center on each axis.</param>
    /// <param name="interval">The distance between searched rows and columns. An interval of one searches every site.</param>
    /// <param name="bounds">The permitted displacements, with exclusive upper edges.</param>
    /// <param name="secondBest">The preceding winner, updated at each improvement.</param>
    /// <returns>The pass winner in full samples.</returns>
    private static Point SearchMeshPass<TCost>(ref TCost cost, Point start, int range, int interval, Rectangle bounds, ref Point? secondBest)
        where TCost : struct, IFullPixelCost, allows ref struct
    {
        start = ClampToBounds(start, bounds);
        Point best = start;
        int bestCost = GetSadCost(ref cost, start);

        // The scan window is the range around the center, cut to the bounds, so no candidate needs a bounds test.
        int minimumRow = Math.Max(-range, bounds.Top - start.Y);
        int maximumRow = Math.Min(range, bounds.Bottom - 1 - start.Y);
        int minimumColumn = Math.Max(-range, bounds.Left - start.X);
        int maximumColumn = Math.Min(range, bounds.Right - 1 - start.X);
        int columnStep = interval > 1 ? interval : 4;
        for (int row = minimumRow; row <= maximumRow; row += interval)
        {
            for (int column = minimumColumn; column <= maximumColumn; column += columnStep)
            {
                // A full unit-step group visits four adjacent columns in order. The partial last group has an exclusive end.
                // Thus the last column of a partial group is not searched.
                int count = interval > 1 ? 1 : column + 3 <= maximumColumn ? 4 : maximumColumn - column;
                for (int index = 0; index < count; index++)
                {
                    Point candidate = new(start.X + column + index, start.Y + row);
                    if (TryImproveSad(ref cost, candidate, cost.GetReferenceIndex(candidate), ref bestCost))
                    {
                        secondBest = best;
                        best = candidate;
                    }
                }
            }
        }

        return best;
    }
}
