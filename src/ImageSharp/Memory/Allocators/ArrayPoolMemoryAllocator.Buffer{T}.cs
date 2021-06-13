// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Contains <see cref="Buffer{T}"/> and <see cref="ManagedByteBuffer"/>.
    /// </summary>
    public partial class ArrayPoolMemoryAllocator
    {
        /// <summary>
        /// The buffer implementation of <see cref="ArrayPoolMemoryAllocator"/>.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer.</typeparam>
        private class Buffer<T> : ManagedBufferBase<T>
            where T : struct
        {
            private readonly ArrayPoolMemoryAllocator allocator;

            /// <summary>
            /// The length of the buffer.
            /// </summary>
            private readonly int length;

            /// <summary>
            /// A weak reference to the source pool.
            /// </summary>
            /// <remarks>
            /// By using a weak reference here, we are making sure that array pools and their retained arrays are always GC-ed
            /// after a call to <see cref="ReleaseRetainedResources"/>, regardless of having buffer instances still being in use.
            /// </remarks>
            private WeakReference<ArrayPool<byte>> sourcePoolReference;

            public Buffer(
                ArrayPoolMemoryAllocator allocator,
                byte[] data,
                int length,
                ArrayPool<byte> sourcePool)
            {
                this.allocator = allocator;
                this.Data = data;
                this.length = length;

                // Only assign reference if using the large pool.
                this.sourcePoolReference = new WeakReference<ArrayPool<byte>>(sourcePool);
            }

            private enum MemoryPressure
            {
                Low = 0,
                Medium = 1,
                High = 2
            }

            /// <summary>
            /// Gets the buffer as a byte array.
            /// </summary>
            protected byte[] Data { get; }

            /// <inheritdoc />
            public override Span<T> GetSpan()
            {
                if (this.Data is null)
                {
                    ThrowObjectDisposedException();
                }
#if SUPPORTS_CREATESPAN
                ref byte r0 = ref MemoryMarshal.GetReference<byte>(this.Data);
                return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref r0), this.length);
#else
                return MemoryMarshal.Cast<byte, T>(this.Data.AsSpan()).Slice(0, this.length);
#endif
            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (!disposing || this.Data is null || this.sourcePoolReference is null)
                {
                    return;
                }

                if (this.sourcePoolReference.TryGetTarget(out ArrayPool<byte> pool))
                {
#if SUPPORTS_GC_MEMORYINFO
                    MemoryPressure pressure = GetMemoryPressure();
                    if (pressure == MemoryPressure.High)
                    {
                        // Don't return. Release everything and let the GC clean everything up.
                        this.allocator.ReleaseRetainedResources();
                    }
                    else
                    {
                        // Standard operations.
                        pool.Return(this.Data);
                    }
#else
                    pool.Return(this.Data);
#endif

                    // Do a callback to see when a buffer was last allocated and clean up
                    // if there's been no activity.
                    var callback = new TimerCallback(OnTime);
                    this.allocator.Timer?.Dispose();

                    // TODO: How long should we wait? currently 5 minutes.
                    this.allocator.Timer = new Timer(callback, this.allocator, 5 * 60 * 1000, Timeout.Infinite);
                }

                this.sourcePoolReference = null;
            }

            protected override object GetPinnableObject() => this.Data;

            private static void OnTime(object state)
            {
                // TODO: This should be based off the set delay.
                if (state is ArrayPoolMemoryAllocator allocator && allocator.Timestamp.AddMinutes(4) < DateTime.UtcNow)
                {
                    allocator.ReleaseRetainedResources();
                }
            }

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ThrowObjectDisposedException()
                => throw new ObjectDisposedException("ArrayPoolMemoryAllocator.Buffer<T>");

#if SUPPORTS_GC_MEMORYINFO
            /// <summary>
            /// Calculates the current memory pressure. Adapted from TlsOverPerCoreLockedStacksArrayPool
            /// in the .NET Runtime - MIT Licensed.
            /// </summary>
            /// <returns>The <see cref="MemoryPressure"/></returns>
            private static MemoryPressure GetMemoryPressure()
            {
                const double highPressureThreshold = .90;       // Percent of GC memory pressure threshold we consider "high"
                const double mediumPressureThreshold = .70;     // Percent of GC memory pressure threshold we consider "medium"

                // TODO: Is there something we can do to get this info in older runtimes?
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
            }
#endif
        }

        /// <summary>
        /// The <see cref="IManagedByteBuffer"/> implementation of <see cref="ArrayPoolMemoryAllocator"/>.
        /// </summary>
        private sealed class ManagedByteBuffer : Buffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(
                ArrayPoolMemoryAllocator allocator,
                byte[] data,
                int length,
                ArrayPool<byte> sourcePool)
                : base(allocator, data, length, sourcePool)
            {
            }

            /// <inheritdoc />
            public byte[] Array => this.Data;
        }
    }
}
