// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// The CostManager is in charge of managing intervals and costs.
/// It caches the different CostCacheInterval, caches the different
/// GetLengthCost(costModel, k) in costCache and the CostInterval's.
/// </summary>
internal sealed class CostManager : IDisposable
{
    private CostInterval? head;

    private const int FreeIntervalsStartCount = 25;

    private readonly Stack<CostInterval> freeIntervals = new(FreeIntervalsStartCount);

    public CostManager(MemoryAllocator memoryAllocator, IMemoryOwner<ushort> distArray, int pixCount, CostModel costModel)
    {
        int costCacheSize = pixCount > BackwardReferenceEncoder.MaxLength ? BackwardReferenceEncoder.MaxLength : pixCount;

        this.CacheIntervals = new List<CostCacheInterval>();
        this.CostCache = new List<double>();
        this.Costs = memoryAllocator.Allocate<float>(pixCount);
        this.DistArray = distArray;
        this.Count = 0;

        for (int i = 0; i < FreeIntervalsStartCount; i++)
        {
            this.freeIntervals.Push(new CostInterval());
        }

        // Fill in the cost cache.
        this.CacheIntervalsSize++;
        this.CostCache.Add(costModel.GetLengthCost(0));
        for (int i = 1; i < costCacheSize; i++)
        {
            this.CostCache.Add(costModel.GetLengthCost(i));

            // Get the number of bound intervals.
            if (this.CostCache[i] != this.CostCache[i - 1])
            {
                this.CacheIntervalsSize++;
            }
        }

        // Fill in the cache intervals.
        CostCacheInterval cur = new()
        {
            Start = 0,
            End = 1,
            Cost = this.CostCache[0]
        };
        this.CacheIntervals.Add(cur);

        for (int i = 1; i < costCacheSize; i++)
        {
            double costVal = this.CostCache[i];
            if (costVal != cur.Cost)
            {
                cur = new CostCacheInterval
                {
                    Start = i,
                    Cost = costVal
                };
                this.CacheIntervals.Add(cur);
            }

            cur.End = i + 1;
        }

        // Set the initial costs high for every pixel as we will keep the minimum.
        this.Costs.GetSpan().Fill(1e38f);
    }

    /// <summary>
    /// Gets or sets the number of stored intervals.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets the costs cache. Contains the GetLengthCost(costModel, k).
    /// </summary>
    public List<double> CostCache { get; }

    public int CacheIntervalsSize { get; }

    public IMemoryOwner<float> Costs { get; }

    public IMemoryOwner<ushort> DistArray { get; }

    public List<CostCacheInterval> CacheIntervals { get; }

    /// <summary>
    /// Update the cost at index i by going over all the stored intervals that overlap with i.
    /// </summary>
    /// <param name="i">The index to update.</param>
    /// <param name="doCleanIntervals">If 'doCleanIntervals' is true, intervals that end before 'i' will be popped.</param>
    public void UpdateCostAtIndex(int i, bool doCleanIntervals)
    {
        CostInterval? current = this.head;
        while (current != null && current.Start <= i)
        {
            CostInterval? next = current.Next;
            if (current.End <= i)
            {
                if (doCleanIntervals)
                {
                    // We have an outdated interval, remove it.
                    this.PopInterval(current);
                }
            }
            else
            {
                this.UpdateCost(i, current.Index, current.Cost);
            }

            current = next;
        }
    }

    /// <summary>
    /// Given a new cost interval defined by its start at position, its length value
    /// and distanceCost, add its contributions to the previous intervals and costs.
    /// If handling the interval or one of its sub-intervals becomes to heavy, its
    /// contribution is added to the costs right away.
    /// </summary>
    public void PushInterval(double distanceCost, int position, int len)
    {
        // If the interval is small enough, no need to deal with the heavy
        // interval logic, just serialize it right away. This constant is empirical.
        int skipDistance = 10;

        Span<float> costs = this.Costs.GetSpan();
        Span<ushort> distArray = this.DistArray.GetSpan();
        if (len < skipDistance)
        {
            for (int j = position; j < position + len; j++)
            {
                int k = j - position;
                float costTmp = (float)(distanceCost + this.CostCache[k]);

                if (costs[j] > costTmp)
                {
                    costs[j] = costTmp;
                    distArray[j] = (ushort)(k + 1);
                }
            }

            return;
        }

        CostInterval? interval = this.head;
        for (int i = 0; i < this.CacheIntervalsSize && this.CacheIntervals[i].Start < len; i++)
        {
            // Define the intersection of the ith interval with the new one.
            int start = position + this.CacheIntervals[i].Start;
            int end = position + (this.CacheIntervals[i].End > len ? len : this.CacheIntervals[i].End);
            float cost = (float)(distanceCost + this.CacheIntervals[i].Cost);

            CostInterval? intervalNext;
            for (; interval != null && interval.Start < end; interval = intervalNext)
            {
                intervalNext = interval.Next;

                // Make sure we have some overlap.
                if (start >= interval.End)
                {
                    continue;
                }

                if (cost >= interval.Cost)
                {
                    // If we are worse than what we already have, add whatever we have so far up to interval.
                    int startNew = interval.End;
                    this.InsertInterval(interval, cost, position, start, interval.Start);
                    start = startNew;
                    if (start >= end)
                    {
                        break;
                    }

                    continue;
                }

                if (start <= interval.Start)
                {
                    if (interval.End <= end)
                    {
                        // We can safely remove the old interval as it is fully included.
                        this.PopInterval(interval);
                    }
                    else
                    {
                        interval.Start = end;
                        break;
                    }
                }
                else
                {
                    if (end < interval.End)
                    {
                        // We have to split the old interval as it fully contains the new one.
                        int endOriginal = interval.End;
                        interval.End = start;
                        this.InsertInterval(interval, interval.Cost, interval.Index, end, endOriginal);
                        break;
                    }

                    interval.End = start;
                }
            }

            // Insert the remaining interval from start to end.
            this.InsertInterval(interval, cost, position, start, end);
        }
    }

    /// <summary>
    /// Pop an interval from the manager.
    /// </summary>
    /// <param name="interval">The interval to remove.</param>
    private void PopInterval(CostInterval? interval)
    {
        if (interval == null)
        {
            return;
        }

        this.ConnectIntervals(interval.Previous, interval.Next);
        this.Count--;

        interval.Next = null;
        interval.Previous = null;
        this.freeIntervals.Push(interval);
    }

    private void InsertInterval(CostInterval? intervalIn, float cost, int position, int start, int end)
    {
        if (start >= end)
        {
            return;
        }

        // TODO: should we use COST_CACHE_INTERVAL_SIZE_MAX?
        CostInterval intervalNew;
        if (this.freeIntervals.Count > 0)
        {
            intervalNew = this.freeIntervals.Pop();
            intervalNew.Cost = cost;
            intervalNew.Start = start;
            intervalNew.End = end;
            intervalNew.Index = position;
        }
        else
        {
            intervalNew = new CostInterval { Cost = cost, Start = start, End = end, Index = position };
        }

        this.PositionOrphanInterval(intervalNew, intervalIn);
        this.Count++;
    }

    /// <summary>
    /// Given a current orphan interval and its previous interval, before
    /// it was orphaned (which can be NULL), set it at the right place in the list
    /// of intervals using the start_ ordering and the previous interval as a hint.
    /// </summary>
    private void PositionOrphanInterval(CostInterval current, CostInterval? previous)
    {
        previous ??= this.head;

        while (previous != null && current.Start < previous.Start)
        {
            previous = previous.Previous;
        }

        while (previous?.Next != null && previous.Next.Start < current.Start)
        {
            previous = previous.Next;
        }

        this.ConnectIntervals(current, previous != null ? previous.Next : this.head);
        this.ConnectIntervals(previous, current);
    }

    /// <summary>
    /// Given two intervals, make 'prev' be the previous one of 'next' in 'manager'.
    /// </summary>
    private void ConnectIntervals(CostInterval? prev, CostInterval? next)
    {
        if (prev != null)
        {
            prev.Next = next;
        }
        else
        {
            this.head = next;
        }

        if (next != null)
        {
            next.Previous = prev;
        }
    }

    /// <summary>
    /// Given the cost and the position that define an interval, update the cost at
    /// pixel 'i' if it is smaller than the previously computed value.
    /// </summary>
    private void UpdateCost(int i, int position, float cost)
    {
        Span<float> costs = this.Costs.GetSpan();
        Span<ushort> distArray = this.DistArray.GetSpan();
        int k = i - position;
        if (costs[i] > cost)
        {
            costs[i] = cost;
            distArray[i] = (ushort)(k + 1);
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.Costs.Dispose();
}
