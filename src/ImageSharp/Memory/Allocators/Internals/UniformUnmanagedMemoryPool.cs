// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        private static int minTrimPeriodMilliseconds = int.MaxValue;
        private static readonly List<WeakReference<UniformUnmanagedMemoryPool>> AllPools = new();
        private static Timer trimTimer;

        private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        private readonly TrimSettings trimSettings;
        private readonly UnmanagedMemoryHandle[] buffers;
        private int index;
        private long lastTrimTimestamp;

        public UniformUnmanagedMemoryPool(int bufferLength, int capacity)
            : this(bufferLength, capacity, TrimSettings.Default)
        {
        }

        public UniformUnmanagedMemoryPool(int bufferLength, int capacity, TrimSettings trimSettings)
        {
            this.trimSettings = trimSettings;
            this.Capacity = capacity;
            this.BufferLength = bufferLength;
            this.buffers = new UnmanagedMemoryHandle[capacity];

            if (trimSettings.Enabled)
            {
                UpdateTimer(trimSettings, this);
#if NETCORE31COMPATIBLE
                Gen2GcCallback.Register(s => ((UniformUnmanagedMemoryPool)s).Trim(), this);
#endif
                this.lastTrimTimestamp = Stopwatch.ElapsedMilliseconds;
            }
        }

        public int BufferLength { get; }

        public int Capacity { get; }

        /// <summary>
        /// Rent a single buffer or return <see cref="UnmanagedMemoryHandle.NullHandle"/> if the pool is full.
        /// </summary>
        public UnmanagedMemoryHandle Rent()
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if the pool is is over it's limit:
            if (this.index == buffersLocal.Length)
            {
                return UnmanagedMemoryHandle.NullHandle;
            }

            UnmanagedMemoryHandle buffer;
            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.index == buffersLocal.Length)
                {
                    return UnmanagedMemoryHandle.NullHandle;
                }

                buffer = buffersLocal[this.index];
                buffersLocal[this.index++] = default;
            }

            if (buffer.IsInvalid)
            {
                buffer = UnmanagedMemoryHandle.Allocate(this.BufferLength);
            }

            return buffer;
        }

        /// <summary>
        /// Rent <paramref name="bufferCount"/> buffers or return 'null' if the pool is full.
        /// </summary>
        public UnmanagedMemoryHandle[] Rent(int bufferCount)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if the pool is is over it's limit:
            if (this.index + bufferCount >= buffersLocal.Length + 1)
            {
                return null;
            }

            UnmanagedMemoryHandle[] result;
            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.index + bufferCount >= buffersLocal.Length + 1)
                {
                    return null;
                }

                result = new UnmanagedMemoryHandle[bufferCount];
                for (int i = 0; i < bufferCount; i++)
                {
                    result[i] = buffersLocal[this.index];
                    buffersLocal[this.index++] = UnmanagedMemoryHandle.NullHandle;
                }
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].IsInvalid)
                {
                    result[i] = UnmanagedMemoryHandle.Allocate(this.BufferLength);
                }
            }

            return result;
        }

        public void Return(UnmanagedMemoryHandle bufferHandle)
        {
            Guard.IsTrue(bufferHandle.IsValid, nameof(bufferHandle), "Returning NullHandle to the pool is not allowed.");
            lock (this.buffers)
            {
                // Check again after taking the lock:
                if (this.index == 0)
                {
                    ThrowReturnedMoreBuffersThanRented(); // DEBUG-only exception
                    bufferHandle.Free();
                    return;
                }

                this.buffers[--this.index] = bufferHandle;
            }
        }

        public void Return(Span<UnmanagedMemoryHandle> bufferHandles)
        {
            lock (this.buffers)
            {
                if (this.index - bufferHandles.Length + 1 <= 0)
                {
                    ThrowReturnedMoreBuffersThanRented();
                    DisposeAll(bufferHandles);
                    return;
                }

                for (int i = bufferHandles.Length - 1; i >= 0; i--)
                {
                    ref UnmanagedMemoryHandle h = ref bufferHandles[i];
                    Guard.IsTrue(h.IsValid, nameof(bufferHandles), "Returning NullHandle to the pool is not allowed.");
                    this.buffers[--this.index] = bufferHandles[i];
                }
            }
        }

        public void Release()
        {
            lock (this.buffers)
            {
                for (int i = this.index; i < this.buffers.Length; i++)
                {
                    UnmanagedMemoryHandle buffer = this.buffers[i];
                    if (buffer.IsInvalid)
                    {
                        break;
                    }

                    buffer.Free();
                    this.buffers[i] = UnmanagedMemoryHandle.NullHandle;
                }
            }
        }

        private static void DisposeAll(Span<UnmanagedMemoryHandle> buffers)
        {
            foreach (UnmanagedMemoryHandle handle in buffers)
            {
                handle.Free();
            }
        }

        // This indicates a bug in the library, however Return() might be called from a finalizer,
        // therefore we should never throw here in production.
        [Conditional("DEBUG")]
        private static void ThrowReturnedMoreBuffersThanRented() =>
            throw new InvalidMemoryOperationException("Returned more buffers then rented");

        private static void UpdateTimer(TrimSettings settings, UniformUnmanagedMemoryPool pool)
        {
            lock (AllPools)
            {
                AllPools.Add(new WeakReference<UniformUnmanagedMemoryPool>(pool));

                // Invoke the timer callback more frequently, than trimSettings.TrimPeriodMilliseconds.
                // We are checking in the callback if enough time passed since the last trimming. If not, we do nothing.
                int period = settings.TrimPeriodMilliseconds / 4;
                if (trimTimer == null)
                {
                    trimTimer = new Timer(_ => TimerCallback(), null, period, period);
                }
                else if (settings.TrimPeriodMilliseconds < minTrimPeriodMilliseconds)
                {
                    trimTimer.Change(period, period);
                }

                minTrimPeriodMilliseconds = Math.Min(minTrimPeriodMilliseconds, settings.TrimPeriodMilliseconds);
            }
        }

        private static void TimerCallback()
        {
            lock (AllPools)
            {
                // Remove lost references from the list:
                for (int i = AllPools.Count - 1; i >= 0; i--)
                {
                    if (!AllPools[i].TryGetTarget(out _))
                    {
                        AllPools.RemoveAt(i);
                    }
                }

                foreach (WeakReference<UniformUnmanagedMemoryPool> weakPoolRef in AllPools)
                {
                    if (weakPoolRef.TryGetTarget(out UniformUnmanagedMemoryPool pool))
                    {
                        pool.Trim();
                    }
                }
            }
        }

        private bool Trim()
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            bool isHighPressure = this.IsHighMemoryPressure();

            if (isHighPressure)
            {
                return this.TrimHighPressure(buffersLocal);
            }

            long millisecondsSinceLastTrim = Stopwatch.ElapsedMilliseconds - this.lastTrimTimestamp;
            if (millisecondsSinceLastTrim > this.trimSettings.TrimPeriodMilliseconds)
            {
                return this.TrimLowPressure(buffersLocal);
            }

            return true;
        }

        private bool TrimHighPressure(UnmanagedMemoryHandle[] buffersLocal)
        {
            lock (buffersLocal)
            {
                // Trim all:
                for (int i = this.index; i < buffersLocal.Length && buffersLocal[i].IsValid; i++)
                {
                    buffersLocal[i].Free();
                    buffersLocal[i] = UnmanagedMemoryHandle.NullHandle;
                }
            }

            return true;
        }

        private bool TrimLowPressure(UnmanagedMemoryHandle[] buffersLocal)
        {
            lock (buffersLocal)
            {
                // Count the buffers in the pool:
                int retainedCount = 0;
                for (int i = this.index; i < buffersLocal.Length && buffersLocal[i].IsValid; i++)
                {
                    retainedCount++;
                }

                // Trim 'trimRate' of 'retainedCount':
                int trimCount = (int)Math.Ceiling(retainedCount * this.trimSettings.Rate);
                int trimStart = this.index + retainedCount - 1;
                int trimStop = this.index + retainedCount - trimCount;
                for (int i = trimStart; i >= trimStop; i--)
                {
                    buffersLocal[i].Free();
                    buffersLocal[i] = UnmanagedMemoryHandle.NullHandle;
                }

                this.lastTrimTimestamp = Stopwatch.ElapsedMilliseconds;
            }

            return true;
        }

        private bool IsHighMemoryPressure()
        {
#if NETCORE31COMPATIBLE
            GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
            return memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * this.trimSettings.HighPressureThresholdRate;
#else
            // We don't have high pressure detection triggering full trimming on other platforms,
            // to counterpart this, the maximum pool size is small.
            return false;
#endif
        }

        public class TrimSettings
        {
            // Trim half of the retained pool buffers every minute.
            public int TrimPeriodMilliseconds { get; set; } = 60_000;

            public float Rate { get; set; } = 0.5f;

            // Be more strict about high pressure on 32 bit.
            public unsafe float HighPressureThresholdRate { get; set; } = sizeof(IntPtr) == 8 ? 0.9f : 0.6f;

            public bool Enabled => this.Rate > 0;

            public static TrimSettings Default => new TrimSettings();
        }
    }
}
