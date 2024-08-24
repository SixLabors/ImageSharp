// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals;

/// <summary>
/// Encapsulates the functionality around allocating and releasing unmanaged memory. NOT a <see cref="SafeHandle"/>.
/// </summary>
internal struct UnmanagedMemoryHandle : IEquatable<UnmanagedMemoryHandle>
{
    // Number of allocation re-attempts when detecting OutOfMemoryException.
    private const int MaxAllocationAttempts = 10;

    // Track allocations for testing purposes:
    private static int totalOutstandingHandles;

    private static long totalOomRetries;

    // A Monitor to wait/signal when we are low on memory.
    private static object? lowMemoryMonitor;

    public static readonly UnmanagedMemoryHandle NullHandle;

    private IntPtr handle;
    private int lengthInBytes;

    private UnmanagedMemoryHandle(IntPtr handle, int lengthInBytes)
    {
        this.handle = handle;
        this.lengthInBytes = lengthInBytes;

        if (lengthInBytes > 0)
        {
            GC.AddMemoryPressure(lengthInBytes);
        }

        Interlocked.Increment(ref totalOutstandingHandles);
    }

    public IntPtr Handle => this.handle;

    public bool IsInvalid => this.Handle == IntPtr.Zero;

    public bool IsValid => this.Handle != IntPtr.Zero;

    public unsafe void* Pointer => (void*)this.Handle;

    /// <summary>
    /// Gets the total outstanding handle allocations for testing purposes.
    /// </summary>
    internal static int TotalOutstandingHandles => totalOutstandingHandles;

    /// <summary>
    /// Gets the total number <see cref="OutOfMemoryException"/>-s retried.
    /// </summary>
    internal static long TotalOomRetries => totalOomRetries;

    public static bool operator ==(UnmanagedMemoryHandle a, UnmanagedMemoryHandle b) => a.Equals(b);

    public static bool operator !=(UnmanagedMemoryHandle a, UnmanagedMemoryHandle b) => !a.Equals(b);

    public static UnmanagedMemoryHandle Allocate(int lengthInBytes)
    {
        IntPtr handle = AllocateHandle(lengthInBytes);
        return new UnmanagedMemoryHandle(handle, lengthInBytes);
    }

    private static IntPtr AllocateHandle(int lengthInBytes)
    {
        int counter = 0;
        IntPtr handle = IntPtr.Zero;
        while (handle == IntPtr.Zero)
        {
            try
            {
                handle = Marshal.AllocHGlobal(lengthInBytes);
            }
            catch (OutOfMemoryException) when (counter < MaxAllocationAttempts)
            {
                // We are low on memory, but expect some memory to be freed soon.
                // Block the thread & retry to avoid OOM.
                counter++;
                Interlocked.Increment(ref totalOomRetries);

                Interlocked.CompareExchange(ref lowMemoryMonitor, new object(), null);
                Monitor.Enter(lowMemoryMonitor);
                Monitor.Wait(lowMemoryMonitor, millisecondsTimeout: 1);
                Monitor.Exit(lowMemoryMonitor);
            }
        }

        return handle;
    }

    public void Free()
    {
        IntPtr h = Interlocked.Exchange(ref this.handle, IntPtr.Zero);

        if (h == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(h);
        Interlocked.Decrement(ref totalOutstandingHandles);
        if (this.lengthInBytes > 0)
        {
            GC.RemoveMemoryPressure(this.lengthInBytes);
        }

        if (Volatile.Read(ref lowMemoryMonitor) != null)
        {
            // We are low on memory. Signal all threads waiting in AllocateHandle().
            Monitor.Enter(lowMemoryMonitor!);
            Monitor.PulseAll(lowMemoryMonitor!);
            Monitor.Exit(lowMemoryMonitor!);
        }

        this.lengthInBytes = 0;
    }

    public bool Equals(UnmanagedMemoryHandle other) => this.handle.Equals(other.handle);

    public override bool Equals(object? obj) => obj is UnmanagedMemoryHandle other && this.Equals(other);

    public override int GetHashCode() => this.handle.GetHashCode();
}
