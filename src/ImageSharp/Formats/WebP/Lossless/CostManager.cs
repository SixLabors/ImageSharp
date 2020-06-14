// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Collections.Generic;
using System.Linq;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// The CostManager is in charge of managing intervals and costs.
    /// It caches the different CostCacheInterval, caches the different
    /// GetLengthCost(cost_model, k) in cost_cache_ and the CostInterval's.
    /// </summary>
    internal class CostManager
    {
        public CostManager(short[] distArray, int pixCount, CostModel costModel)
        {
            int costCacheSize = (pixCount > BackwardReferenceEncoder.MaxLength) ? BackwardReferenceEncoder.MaxLength : pixCount;

            this.Intervals = new List<CostInterval>();
            this.CacheIntervals = new List<CostCacheInterval>();
            this.CostCache = new List<double>();
            this.Costs = new float[pixCount];
            this.DistArray = distArray;
            this.Count = 0;

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
            var cur = new CostCacheInterval()
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
                    cur = new CostCacheInterval()
                    {
                        Start = i,
                        Cost = costVal
                    };
                    this.CacheIntervals.Add(cur);
                }

                cur.End = i + 1;
            }

            // Set the initial costs_ high for every pixel as we will keep the minimum.
            for (int i = 0; i < pixCount; i++)
            {
                this.Costs[i] = 1e38f;
            }
        }

        /// <summary>
        /// Gets the number of stored intervals.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the costs cache. Contains the GetLengthCost(costModel, k).
        /// </summary>
        public List<double> CostCache { get; }

        public int CacheIntervalsSize { get; }

        public float[] Costs { get; }

        public short[] DistArray { get; }

        public List<CostInterval> Intervals { get; }

        public List<CostCacheInterval> CacheIntervals { get; }

        /// <summary>
        /// Update the cost at index i by going over all the stored intervals that overlap with i.
        /// </summary>
        /// <param name="i">The index to update.</param>
        /// <param name="doCleanIntervals">If 'doCleanIntervals' is true, intervals that end before 'i' will be popped.</param>
        public void UpdateCostAtIndex(int i, bool doCleanIntervals)
        {
            var indicesToRemove = new List<int>();
            using List<CostInterval>.Enumerator intervalEnumerator = this.Intervals.GetEnumerator();
            while (intervalEnumerator.MoveNext() && intervalEnumerator.Current.Start <= i)
            {
                if (intervalEnumerator.Current.End <= i)
                {
                    if (doCleanIntervals)
                    {
                        // We have an outdated interval, remove it.
                        indicesToRemove.Add(i);
                    }
                }
                else
                {
                    this.UpdateCost(i, intervalEnumerator.Current.Index, intervalEnumerator.Current.Cost);
                }
            }

            foreach (int index in indicesToRemove.OrderByDescending(i => i))
            {
                this.Intervals.RemoveAt(index);
            }
        }

        /// <summary>
        /// Given a new cost interval defined by its start at position, its length value
        /// and distanceCost, add its contributions to the previous intervals and costs.
        /// If handling the interval or one of its subintervals becomes to heavy, its
        /// contribution is added to the costs right away.
        /// </summary>
        public void PushInterval(double distanceCost, int position, int len)
        {
            // If the interval is small enough, no need to deal with the heavy
            // interval logic, just serialize it right away. This constant is empirical.
            int skipDistance = 10;

            if (len < skipDistance)
            {
                for (int j = position; j < position + len; ++j)
                {
                    int k = j - position;
                    float costTmp = (float)(distanceCost + this.CostCache[k]);

                    if (this.Costs[j] > costTmp)
                    {
                        this.Costs[j] = costTmp;
                        this.DistArray[j] = (short)(k + 1);
                    }
                }

                return;
            }

            for (int i = 0; i < this.CacheIntervalsSize && this.CacheIntervals[i].Start < len; i++)
            {
                // Define the intersection of the ith interval with the new one.
                int start = position + this.CacheIntervals[i].Start;
                int end = position + (this.CacheIntervals[i].End > len ? len : this.CacheIntervals[i].End);
                float cost = (float)(distanceCost + this.CacheIntervals[i].Cost);

                var idx = i;
                CostCacheInterval interval = this.CacheIntervals[idx];
                var indicesToRemove = new List<int>();
                for (; interval.Start < end; idx++)
                {
                    // Make sure we have some overlap.
                    if (start >= interval.End)
                    {
                        continue;
                    }

                    if (cost >= interval.Cost)
                    {
                        int startNew = interval.End;
                        this.InsertInterval(cost, position, start, interval.Start);
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
                            indicesToRemove.Add(idx);
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
                            int endOriginal = interval.End;
                            interval.End = start;
                            this.InsertInterval(interval.Cost, idx, end, endOriginal);
                            break;
                        }
                        else
                        {
                            interval.End = start;
                        }
                    }
                }

                foreach (int indice in indicesToRemove.OrderByDescending(i => i))
                {
                    this.Intervals.RemoveAt(indice);
                }

                // Insert the remaining interval from start to end.
                this.InsertInterval(cost, position, start, end);
            }
        }

        private void InsertInterval(double cost, int position, int start, int end)
        {
            // TODO: use COST_CACHE_INTERVAL_SIZE_MAX

            var interval = new CostCacheInterval()
            {
                Cost = cost,
                Start = start,
                End = end
            };

            this.CacheIntervals.Insert(position, interval);
        }

        /// <summary>
        /// Given the cost and the position that define an interval, update the cost at
        /// pixel 'i' if it is smaller than the previously computed value.
        /// </summary>
        private void UpdateCost(int i, int position, float cost)
        {
            int k = i - position;
            if (this.Costs[i] > cost)
            {
                this.Costs[i] = cost;
                this.DistArray[i] = (short)(k + 1);
            }
        }
    }
}
