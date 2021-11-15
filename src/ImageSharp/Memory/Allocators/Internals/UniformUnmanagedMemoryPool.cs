// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        private readonly TrimSettings trimSettings;
        private UnmanagedMemoryHandle[] buffers;
        private int index;
        private Timer trimTimer;
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
                // Invoke the timer callback more frequently, than trimSettings.TrimPeriodMilliseconds,
                // and also invoke it on Gen 2 GC.
                // We are checking in the callback if enough time passed since the last trimming. If not, we do nothing.
                var weakPoolRef = new WeakReference<UniformUnmanagedMemoryPool>(this);
                this.trimTimer = new Timer(
                    s => TimerCallback((WeakReference<UniformUnmanagedMemoryPool>)s),
                    weakPoolRef,
                    this.trimSettings.TrimPeriodMilliseconds / 4,
                    this.trimSettings.TrimPeriodMilliseconds / 4);

#if NETCORE31COMPATIBLE
                Gen2GcCallback.Register(s => ((UniformUnmanagedMemoryPool)s).Trim(), this);
#endif
                this.lastTrimTimestamp = Stopwatch.ElapsedMilliseconds;
            }
        }

        public int BufferLength { get; }

        public int Capacity { get; }

        public UnmanagedMemoryHandle Rent()
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if the pool is released or is over limit:
            if (buffersLocal == null || this.index == buffersLocal.Length)
            {
                return null;
            }

            UnmanagedMemoryHandle buffer;

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null || this.index == buffersLocal.Length)
                {
                    return null;
                }

                buffer = buffersLocal[this.index];
                buffersLocal[this.index++] = null;
            }

            if (buffer == null)
            {
                buffer = UnmanagedMemoryHandle.Allocate(this.BufferLength);
            }

            if (buffer.IsInvalid)
            {
                throw new InvalidOperationException("Rending disposed handle :O !!!");
            }

            return buffer;
        }

        public UnmanagedMemoryHandle[] Rent(int bufferCount)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if the pool is released or is over limit:
            if (buffersLocal == null || this.index + bufferCount >= buffersLocal.Length + 1)
            {
                return null;
            }

            UnmanagedMemoryHandle[] result;
            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null || this.index + bufferCount >= buffersLocal.Length + 1)
                {
                    return null;
                }

                result = new UnmanagedMemoryHandle[bufferCount];
                for (int i = 0; i < bufferCount; i++)
                {
                    result[i] = buffersLocal[this.index];
                    buffersLocal[this.index++] = null;

                    if (result[i]?.IsInvalid == true)
                    {
                        throw new InvalidOperationException("Renting disposed handle :O !!!");
                    }
                }
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == null)
                {
                    result[i] = UnmanagedMemoryHandle.Allocate(this.BufferLength);
                }
            }

            return result;
        }

        public void Return(UnmanagedMemoryHandle buffer)
        {
            if (buffer.IsInvalid)
            {
                throw new InvalidOperationException("Returning a disposed handle :O !!!");
            }

            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            if (buffersLocal == null)
            {
                buffer.Dispose();
                return;
            }

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null)
                {
                    buffer.Dispose();
                    return;
                }

                if (this.index == 0)
                {
                    ThrowReturnedMoreBuffersThanRented(); // DEBUG-only exception
                    buffer.Dispose();
                    return;
                }

                this.buffers[--this.index] = buffer;
            }
        }

        public void Return(Span<UnmanagedMemoryHandle> buffers)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            if (buffersLocal == null)
            {
                DisposeAll(buffers);
                return;
            }

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null)
                {
                    DisposeAll(buffers);
                    return;
                }

                if (this.index - buffers.Length + 1 <= 0)
                {
                    ThrowReturnedMoreBuffersThanRented();
                    DisposeAll(buffers);
                    return;
                }

                for (int i = buffers.Length - 1; i >= 0; i--)
                {
                    if (buffers[i].IsInvalid)
                    {
                        throw new InvalidOperationException("Returning a disposed handle :O !!!");
                    }

                    buffersLocal[--this.index] = buffers[i];
                }
            }
        }

        public void Release()
        {
            this.trimTimer?.Dispose();
            this.trimTimer = null;
            UnmanagedMemoryHandle[] oldBuffers = Interlocked.Exchange(ref this.buffers, null);
            DebugGuard.NotNull(oldBuffers, nameof(oldBuffers));
            DisposeAll(oldBuffers);
        }

        private static void DisposeAll(Span<UnmanagedMemoryHandle> buffers)
        {
            foreach (UnmanagedMemoryHandle handle in buffers)
            {
                handle?.Dispose();
            }
        }

        private unsafe Span<byte> GetSpan(UnmanagedMemoryHandle h) =>
            new Span<byte>((byte*)h.DangerousGetHandle(), this.BufferLength);

        // This indicates a bug in the library, however Return() might be called from a finalizer,
        // therefore we should never throw here in production.
        [Conditional("DEBUG")]
        private static void ThrowReturnedMoreBuffersThanRented() =>
            throw new InvalidMemoryOperationException("Returned more buffers then rented");

        private static void TimerCallback(WeakReference<UniformUnmanagedMemoryPool> weakPoolRef)
        {
            if (weakPoolRef.TryGetTarget(out UniformUnmanagedMemoryPool pool))
            {
                pool.Trim();
            }
        }

        private bool Trim()
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            if (buffersLocal == null)
            {
                return false;
            }

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
                if (this.buffers == null)
                {
                    return false;
                }

                // Trim all:
                for (int i = this.index; i < buffersLocal.Length && buffersLocal[i] != null; i++)
                {
                    buffersLocal[i].Dispose();
                    buffersLocal[i] = null;
                }
            }

            return true;
        }

        private bool TrimLowPressure(UnmanagedMemoryHandle[] buffersLocal)
        {
            lock (buffersLocal)
            {
                if (this.buffers == null)
                {
                    return false;
                }

                // Count the buffers in the pool:
                int retainedCount = 0;
                for (int i = this.index; i < buffersLocal.Length && buffersLocal[i] != null; i++)
                {
                    retainedCount++;
                }

                // Trim 'trimRate' of 'retainedCount':
                int trimCount = (int)Math.Ceiling(retainedCount * this.trimSettings.Rate);
                int trimStart = this.index + retainedCount - 1;
                int trimStop = this.index + retainedCount - trimCount;
                for (int i = trimStart; i >= trimStop; i--)
                {
                    buffersLocal[i].Dispose();
                    buffersLocal[i] = null;
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
