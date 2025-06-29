// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Memory.Internals;

// CriticalFinalizerObject:
// In case UniformUnmanagedMemoryPool is finalized, we prefer to run its finalizer after the guard finalizers,
// but we should not rely on this.
internal partial class UniformUnmanagedMemoryPool : System.Runtime.ConstrainedExecution.CriticalFinalizerObject
{
    private static int minTrimPeriodMilliseconds = int.MaxValue;
    private static readonly List<WeakReference<UniformUnmanagedMemoryPool>> AllPools = new();
    private static Timer? trimTimer;

    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

    private readonly TrimSettings trimSettings;
    private readonly UnmanagedMemoryHandle[] buffers;
    private int index;
    private long lastTrimTimestamp;
    private int finalized;

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
            Gen2GcCallback.Register(s => ((UniformUnmanagedMemoryPool)s).Trim(), this);
            this.lastTrimTimestamp = Stopwatch.ElapsedMilliseconds;
        }
    }

    // We don't want UniformUnmanagedMemoryPool and MemoryAllocator to be IDisposable,
    // since the types don't really match Disposable semantics.
    // If a user wants to drop a MemoryAllocator after they finished using it, they should call allocator.ReleaseRetainedResources(),
    // which normally should free the already returned (!) buffers.
    // However in case if this doesn't happen, we need the retained memory to be freed by the finalizer.
    ~UniformUnmanagedMemoryPool()
    {
        Interlocked.Exchange(ref this.finalized, 1);
        this.TrimAll(this.buffers);
    }

    public int BufferLength { get; }

    public int Capacity { get; }

    private bool Finalized => this.finalized == 1;

    /// <summary>
    /// Rent a single buffer. If the pool is full, return <see cref="UnmanagedMemoryHandle.NullHandle"/>.
    /// </summary>
    public UnmanagedMemoryHandle Rent()
    {
        UnmanagedMemoryHandle[] buffersLocal = this.buffers;

        // Avoid taking the lock if the pool is is over it's limit:
        if (this.index == buffersLocal.Length || this.Finalized)
        {
            return UnmanagedMemoryHandle.NullHandle;
        }

        UnmanagedMemoryHandle buffer;
        lock (buffersLocal)
        {
            // Check again after taking the lock:
            if (this.index == buffersLocal.Length || this.Finalized)
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
    public UnmanagedMemoryHandle[]? Rent(int bufferCount)
    {
        UnmanagedMemoryHandle[] buffersLocal = this.buffers;

        // Avoid taking the lock if the pool is is over it's limit:
        if (this.index + bufferCount >= buffersLocal.Length + 1 || this.Finalized)
        {
            return null;
        }

        UnmanagedMemoryHandle[] result;
        lock (buffersLocal)
        {
            // Check again after taking the lock:
            if (this.index + bufferCount >= buffersLocal.Length + 1 || this.Finalized)
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

    // The Return methods return false if and only if:
    // (1) More buffers are returned than rented OR
    // (2) The pool has been finalized.
    // This is defensive programming, since neither of the cases should happen normally
    // (case 1 would be a programming mistake in the library, case 2 should be prevented by the CriticalFinalizerObject contract),
    // so we throw in Debug instead of returning false.
    // In Release, the caller should Free() the handles if false is returned to avoid memory leaks.
    public bool Return(UnmanagedMemoryHandle bufferHandle)
    {
        Guard.IsTrue(bufferHandle.IsValid, nameof(bufferHandle), "Returning NullHandle to the pool is not allowed.");
        lock (this.buffers)
        {
            if (this.Finalized || this.index == 0)
            {
                this.DebugThrowInvalidReturn();
                return false;
            }

            this.buffers[--this.index] = bufferHandle;
        }

        return true;
    }

    public bool Return(Span<UnmanagedMemoryHandle> bufferHandles)
    {
        lock (this.buffers)
        {
            if (this.Finalized || this.index - bufferHandles.Length + 1 <= 0)
            {
                this.DebugThrowInvalidReturn();
                return false;
            }

            for (int i = bufferHandles.Length - 1; i >= 0; i--)
            {
                ref UnmanagedMemoryHandle h = ref bufferHandles[i];
                Guard.IsTrue(h.IsValid, nameof(bufferHandles), "Returning NullHandle to the pool is not allowed.");
                this.buffers[--this.index] = h;
            }
        }

        return true;
    }

    public void Release()
    {
        lock (this.buffers)
        {
            for (int i = this.index; i < this.buffers.Length; i++)
            {
                ref UnmanagedMemoryHandle buffer = ref this.buffers[i];
                if (buffer.IsInvalid)
                {
                    break;
                }

                buffer.Free();
            }
        }
    }

    [Conditional("DEBUG")]
    private void DebugThrowInvalidReturn()
    {
        if (this.Finalized)
        {
            throw new ObjectDisposedException(
                nameof(UniformUnmanagedMemoryPool),
                "Invalid handle return to the pool! The pool has been finalized.");
        }

        throw new InvalidOperationException(
            "Invalid handle return to the pool! Returning more buffers than rented.");
    }

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
                if (weakPoolRef.TryGetTarget(out UniformUnmanagedMemoryPool? pool))
                {
                    pool.Trim();
                }
            }
        }
    }

    private bool Trim()
    {
        if (this.Finalized)
        {
            return false;
        }

        UnmanagedMemoryHandle[] buffersLocal = this.buffers;

        bool isHighPressure = this.IsHighMemoryPressure();

        if (isHighPressure)
        {
            this.TrimAll(buffersLocal);
            return true;
        }

        long millisecondsSinceLastTrim = Stopwatch.ElapsedMilliseconds - this.lastTrimTimestamp;
        if (millisecondsSinceLastTrim > this.trimSettings.TrimPeriodMilliseconds)
        {
            return this.TrimLowPressure(buffersLocal);
        }

        return true;
    }

    private void TrimAll(UnmanagedMemoryHandle[] buffersLocal)
    {
        lock (buffersLocal)
        {
            // Trim all:
            for (int i = this.index; i < buffersLocal.Length && buffersLocal[i].IsValid; i++)
            {
                buffersLocal[i].Free();
            }
        }
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
            }

            this.lastTrimTimestamp = Stopwatch.ElapsedMilliseconds;
        }

        return true;
    }

    private bool IsHighMemoryPressure()
    {
        GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
        return memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * this.trimSettings.HighPressureThresholdRate;
    }

    public class TrimSettings
    {
        // Trim half of the retained pool buffers every minute.
        public int TrimPeriodMilliseconds { get; set; } = 60_000;

        public float Rate { get; set; } = 0.5f;

        // Be more strict about high pressure on 32 bit.
        public unsafe float HighPressureThresholdRate { get; set; } = sizeof(IntPtr) == 8 ? 0.9f : 0.6f;

        public bool Enabled => this.Rate > 0;

        public static TrimSettings Default => new();
    }
}
