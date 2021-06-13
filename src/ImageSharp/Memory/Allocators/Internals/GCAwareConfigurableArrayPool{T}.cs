// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Allocators.Internals
{
    /// <summary>
    /// Represents an array pool that can be configured.
    /// The pool is also GC aware and will perform automatic trimming based upon the current memeory pressure.
    /// Adapted from the NET Runtime. MIT Licensed.
    /// </summary>
    /// <typeparam name="T">The type of buffer </typeparam>
    internal sealed class GCAwareConfigurableArrayPool<T> : ArrayPool<T>
    {
        private readonly Bucket[] buckets;
        private int callbackCreated;

        internal GCAwareConfigurableArrayPool(int maxArrayLength, int maxArraysPerBucket)
        {
            if (maxArrayLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArrayLength));
            }

            if (maxArraysPerBucket <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArraysPerBucket));
            }

            // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
            // Constrain the actual max used to those values.
            const int minimumArrayLength = 0x10, maximumArrayLength = 0x40000000;
            if (maxArrayLength > maximumArrayLength)
            {
                maxArrayLength = maximumArrayLength;
            }
            else if (maxArrayLength < minimumArrayLength)
            {
                maxArrayLength = minimumArrayLength;
            }

            // Create the buckets.
            int poolId = this.Id;
            int maxBuckets = Utilities.SelectBucketIndex(maxArrayLength);
            var buckets = new Bucket[maxBuckets + 1];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new Bucket(Utilities.GetMaxSizeForBucket(i), maxArraysPerBucket, poolId);
            }

            this.buckets = buckets;
        }

        private enum MemoryPressure
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        /// <summary>Gets an ID for the pool to use with events.</summary>
        private int Id => this.GetHashCode();

        /// <inheritdoc/>
        public override T[] Rent(int minimumLength)
        {
            // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
            // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
            // to be usable in general instead of using `new`, even for computed lengths.
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLength));
            }
            else if (minimumLength == 0)
            {
                // No need for events with the empty array.  Our pool is effectively infinite
                // and we'll never allocate for rents and never store for returns.
                return Array.Empty<T>();
            }

            ArrayPoolEventSource log = ArrayPoolEventSource.Log;
            T[] buffer;

            int index = Utilities.SelectBucketIndex(minimumLength);
            if (index < this.buckets.Length)
            {
                // Search for an array starting at the 'index' bucket. If the bucket is empty, bump up to the
                // next higher bucket and try that one, but only try at most a few buckets.
                const int maxBucketsToTry = 2;
                int i = index;
                do
                {
                    // Attempt to rent from the bucket.  If we get a buffer from it, return it.
                    buffer = this.buckets[i].Rent();
                    if (buffer != null)
                    {
                        if (log.IsEnabled())
                        {
                            log.BufferRented(buffer.GetHashCode(), buffer.Length, this.Id, this.buckets[i].Id);
                        }

                        return buffer;
                    }
                }
                while (++i < this.buckets.Length && i != index + maxBucketsToTry);

                // The pool was exhausted for this buffer size.  Allocate a new buffer with a size corresponding
                // to the appropriate bucket.
                buffer = new T[this.buckets[index].BufferLength];
            }
            else
            {
                // The request was for a size too large for the pool.  Allocate an array of exactly the requested length.
                // When it's returned to the pool, we'll simply throw it away.
                buffer = new T[minimumLength];
            }

            if (log.IsEnabled())
            {
                int bufferId = buffer.GetHashCode();
                log.BufferRented(bufferId, buffer.Length, this.Id, ArrayPoolEventSource.NoBucketId);

                ArrayPoolEventSource.BufferAllocatedReason reason = index >= this.buckets.Length
                    ? ArrayPoolEventSource.BufferAllocatedReason.OverMaximumSize
                    : ArrayPoolEventSource.BufferAllocatedReason.PoolExhausted;
                log.BufferAllocated(
                    bufferId,
                    buffer.Length,
                    this.Id,
                    ArrayPoolEventSource.NoBucketId,
                    reason);
            }

            return buffer;
        }

        /// <inheritdoc/>
        public override void Return(T[] array, bool clearArray = false)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            else if (array.Length == 0)
            {
                // Ignore empty arrays.  When a zero-length array is rented, we return a singleton
                // rather than actually taking a buffer out of the lowest bucket.
                return;
            }

            // Determine with what bucket this array length is associated
            int bucket = Utilities.SelectBucketIndex(array.Length);

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            bool haveBucket = bucket < this.buckets.Length;
            if (haveBucket)
            {
                // Clear the array if the user requests
                if (clearArray)
                {
                    Array.Clear(array, 0, array.Length);
                }

                // Return the buffer to its bucket.  In the future, we might consider having Return return false
                // instead of dropping a bucket, in which case we could try to return to a lower-sized bucket,
                // just as how in Rent we allow renting from a higher-sized bucket.
                this.buckets[bucket].Return(array);

                if (Interlocked.Exchange(ref this.callbackCreated, 1) != 1)
                {
                    Gen2GcCallback.Register(Gen2GcCallbackFunc, this);
                }
            }

            // Log that the buffer was returned
            ArrayPoolEventSource log = ArrayPoolEventSource.Log;
            if (log.IsEnabled())
            {
                int bufferId = array.GetHashCode();
                log.BufferReturned(bufferId, array.Length, this.Id);
                if (!haveBucket)
                {
                    log.BufferDropped(bufferId, array.Length, this.Id, ArrayPoolEventSource.NoBucketId, ArrayPoolEventSource.BufferDroppedReason.Full);
                }
            }
        }

        /// <summary>
        /// Allows the manual trimming of the pool.
        /// </summary>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool Trim()
        {
            int milliseconds = Environment.TickCount;
            MemoryPressure pressure = GetMemoryPressure();

            Bucket[] allBuckets = this.buckets;
            for (int i = 0; i < allBuckets.Length; i++)
            {
                allBuckets[i]?.Trim((uint)milliseconds, pressure);
            }

            return true;
        }

        /// <summary>
        /// This is the static function that is called from the gen2 GC callback.
        /// The input object is the instance we want the callback on.
        /// </summary>
        /// <param name="target">The callback target.</param>
        /// <remarks>
        /// The reason that we make this function static and take the instance as a parameter is that
        /// we would otherwise root the instance to the Gen2GcCallback object, leaking the instance even when
        /// the application no longer needs it.
        /// </remarks>
        private static bool Gen2GcCallbackFunc(object target)
            => ((GCAwareConfigurableArrayPool<T>)target).Trim();

        private static MemoryPressure GetMemoryPressure()
        {
#if SUPPORTS_GC_MEMORYINFO
            const double highPressureThreshold = .90;       // Percent of GC memory pressure threshold we consider "high"
            const double mediumPressureThreshold = .70;     // Percent of GC memory pressure threshold we consider "medium"

            GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
            if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * highPressureThreshold)
            {
                return MemoryPressure.High;
            }
            else if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * mediumPressureThreshold)
            {
                return MemoryPressure.Medium;
            }

            return MemoryPressure.Low;
#else
            return MemoryPressure.Medium;
#endif
        }

        /// <summary>
        /// Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.
        /// </summary>
        private sealed class Bucket
        {
            private readonly T[][] buffers;
            private readonly int poolId;
            private readonly int numberOfBuffers;

            /// <summary>
            /// Do not make this readonly; it's a mutable struct
            /// </summary>
            private SpinLock spinLock;
            private int index;
            private uint firstItemMS;

            /// <summary>
            /// Initializes a new instance of the <see cref="Bucket"/> class.
            /// </summary>
            /// <param name="bufferLength">The length of each buffer.</param>
            /// <param name="numberOfBuffers">The number of buffers each of <paramref name="bufferLength"/>.</param>
            /// <param name="poolId">The pool id.</param>
            internal Bucket(int bufferLength, int numberOfBuffers, int poolId)
            {
                this.spinLock = new SpinLock(Debugger.IsAttached); // only enable thread tracking if debugger is attached; it adds non-trivial overheads to Enter/Exit
                this.buffers = new T[numberOfBuffers][];
                this.BufferLength = bufferLength;
                this.numberOfBuffers = numberOfBuffers;
                this.poolId = poolId;
            }

            /// <summary>Gets an ID for the bucket to use with events.</summary>
            internal int Id => this.GetHashCode();

            internal int BufferLength { get; }

            internal T[] Rent()
            {
                T[][] buffers = this.buffers;
                T[] buffer = null;

                // While holding the lock, grab whatever is at the next available index and
                // update the index.  We do as little work as possible while holding the spin
                // lock to minimize contention with other threads.  The try/finally is
                // necessary to properly handle thread aborts on platforms which have them.
                bool lockTaken = false, allocateBuffer = false;
                try
                {
                    this.spinLock.Enter(ref lockTaken);

                    if (this.index < buffers.Length)
                    {
                        buffer = buffers[this.index];
                        buffers[this.index++] = null;
                        allocateBuffer = buffer == null;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.spinLock.Exit(false);
                    }
                }

                // While we were holding the lock, we grabbed whatever was at the next available index, if
                // there was one. If we tried and if we got back null, that means we hadn't yet allocated
                // for that slot, in which case we should do so now.
                if (allocateBuffer)
                {
                    if (this.index == 0)
                    {
                        // Stash the time the first item was added.
                        this.firstItemMS = (uint)Environment.TickCount;
                    }

                    buffer = new T[this.BufferLength];

                    ArrayPoolEventSource log = ArrayPoolEventSource.Log;
                    if (log.IsEnabled())
                    {
                        log.BufferAllocated(
                            buffer.GetHashCode(),
                            this.BufferLength,
                            this.poolId,
                            this.Id,
                            ArrayPoolEventSource.BufferAllocatedReason.Pooled);
                    }
                }

                return buffer;
            }

            internal void Return(T[] array)
            {
                // Check to see if the buffer is the correct size for this bucket
                if (array.Length != this.BufferLength)
                {
                    throw new ArgumentException("Buffer not from pool.", nameof(array));
                }

                bool returned;

                // While holding the spin lock, if there's room available in the bucket,
                // put the buffer into the next available slot.  Otherwise, we just drop it.
                // The try/finally is necessary to properly handle thread aborts on platforms
                // which have them.
                bool lockTaken = false;
                try
                {
                    this.spinLock.Enter(ref lockTaken);

                    returned = this.index != 0;
                    if (returned)
                    {
                        this.buffers[--this.index] = array;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.spinLock.Exit(false);
                    }
                }

                if (!returned)
                {
                    ArrayPoolEventSource log = ArrayPoolEventSource.Log;
                    if (log.IsEnabled())
                    {
                        log.BufferDropped(array.GetHashCode(), this.BufferLength, this.poolId, this.Id, ArrayPoolEventSource.BufferDroppedReason.Full);
                    }
                }
            }

            internal void Trim(uint tickCount, MemoryPressure pressure)
            {
                const uint trimAfterMS = 60 * 1000;       // Trim after 60 seconds for low/moderate pressure
                const uint highTrimAfterMS = 10 * 1000;   // Trim after 10 seconds for high pressure
                const uint refreshMS = trimAfterMS / 4;   // Time bump after trimming (1/4 trim time)
                const int lowTrimCount = 1;               // Trim one item when pressure is low
                const int mediumTrimCount = 2;            // Trim two items when pressure is moderate
                int highTrimCount = this.numberOfBuffers; // Trim all items when pressure is high
                const int largeBucket = 16384;            // If the bucket is larger than this we'll trim an extra when under high pressure
                const int moderateTypeSize = 16;          // If T is larger than this we'll trim an extra when under high pressure
                const int largeTypeSize = 32;             // If T is larger than this we'll trim an extra (additional) when under high pressure

                if (this.index == 0)
                {
                    return;
                }

                bool lockTaken = false;
                try
                {
                    this.spinLock.Enter(ref lockTaken);

                    uint trimTicks = pressure == MemoryPressure.High
                        ? highTrimAfterMS
                        : trimAfterMS;

                    if ((this.index > 0 && this.firstItemMS > tickCount) || (tickCount - this.firstItemMS) > trimTicks)
                    {
                        // We've wrapped the tick count or elapsed enough time since the
                        // first item went into the array. Drop some items so they can
                        // be collected.
                        int trimCount = lowTrimCount;
                        int bucketSize = this.BufferLength;

                        switch (pressure)
                        {
                            case MemoryPressure.High:
                                trimCount = highTrimCount;

                                // When pressure is high, aggressively trim larger arrays.
                                if (bucketSize > largeBucket)
                                {
                                    trimCount++;
                                }

                                if (Unsafe.SizeOf<T>() > moderateTypeSize)
                                {
                                    trimCount++;
                                }

                                if (Unsafe.SizeOf<T>() > largeTypeSize)
                                {
                                    trimCount++;
                                }

                                break;
                            case MemoryPressure.Medium:
                                trimCount = mediumTrimCount;
                                break;
                        }

                        while (this.index > 0 && trimCount-- > 0)
                        {
                            this.buffers[--this.index] = null;
                        }

                        if (this.index > 0 && this.firstItemMS < uint.MaxValue - refreshMS)
                        {
                            // Give the remaining items a bit more time
                            this.firstItemMS += refreshMS;
                        }
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.spinLock.Exit(false);
                    }
                }
            }
        }
    }
}
