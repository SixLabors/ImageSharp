// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

internal abstract partial class MemoryGroup<T>
{
    /// <summary>
    /// A <see cref="MemoryGroup{T}"/> implementation that owns the underlying memory buffers.
    /// </summary>
    public sealed class Owned : MemoryGroup<T>, IEnumerable<Memory<T>>
    {
        private IMemoryOwner<T>[]? memoryOwners;
        private RefCountedMemoryLifetimeGuard? groupLifetimeGuard;

        public Owned(IMemoryOwner<T>[] memoryOwners, int bufferLength, long totalLength, bool swappable)
            : base(bufferLength, totalLength)
        {
            this.memoryOwners = memoryOwners;
            this.Swappable = swappable;
            this.View = new(this);
            this.memoryGroupSpanCache = MemoryGroupSpanCache.Create(memoryOwners);
        }

        public Owned(
            UniformUnmanagedMemoryPool pool,
            UnmanagedMemoryHandle[] pooledHandles,
            int bufferLength,
            long totalLength,
            int sizeOfLastBuffer,
            AllocationOptions options)
            : this(CreateBuffers(pooledHandles, bufferLength, sizeOfLastBuffer, options), bufferLength, totalLength, true) =>
            this.groupLifetimeGuard = pool.CreateGroupLifetimeGuard(pooledHandles);

        public bool Swappable { get; }

        private bool IsDisposed => this.memoryOwners == null;

        public override int Count
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                this.EnsureNotDisposed();
                return this.memoryOwners.Length;
            }
        }

        public override Memory<T> this[int index]
        {
            get
            {
                this.EnsureNotDisposed();
                return this.memoryOwners[index].Memory;
            }
        }

        private static IMemoryOwner<T>[] CreateBuffers(
            UnmanagedMemoryHandle[] pooledBuffers,
            int bufferLength,
            int sizeOfLastBuffer,
            AllocationOptions options)
        {
            IMemoryOwner<T>[]? result = new IMemoryOwner<T>[pooledBuffers.Length];
            for (int i = 0; i < pooledBuffers.Length - 1; i++)
            {
                ObservedBuffer? currentBuffer = ObservedBuffer.Create(pooledBuffers[i], bufferLength, options);
                result[i] = currentBuffer;
            }

            ObservedBuffer? lastBuffer = ObservedBuffer.Create(pooledBuffers[pooledBuffers.Length - 1], sizeOfLastBuffer, options);
            result[result.Length - 1] = lastBuffer;
            return result;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override MemoryGroupEnumerator<T> GetEnumerator() => new(this);

        public override void IncreaseRefCounts()
        {
            this.EnsureNotDisposed();

            if (this.groupLifetimeGuard != null)
            {
                this.groupLifetimeGuard.AddRef();
            }
            else
            {
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    if (memoryOwner is IRefCounted unmanagedBuffer)
                    {
                        unmanagedBuffer.AddRef();
                    }
                }
            }
        }

        public override void DecreaseRefCounts()
        {
            this.EnsureNotDisposed();
            if (this.groupLifetimeGuard != null)
            {
                this.groupLifetimeGuard.ReleaseRef();
            }
            else
            {
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners)
                {
                    if (memoryOwner is IRefCounted unmanagedBuffer)
                    {
                        unmanagedBuffer.ReleaseRef();
                    }
                }
            }
        }

        public override void RecreateViewAfterSwap()
        {
            this.View.Invalidate();
            this.View = new(this);
        }

        /// <inheritdoc/>
        IEnumerator<Memory<T>> IEnumerable<Memory<T>>.GetEnumerator()
        {
            this.EnsureNotDisposed();
            return this.memoryOwners.Select(mo => mo.Memory).GetEnumerator();
        }

        public override void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            this.View.Invalidate();

            if (this.groupLifetimeGuard != null)
            {
                this.groupLifetimeGuard.Dispose();
            }
            else
            {
                foreach (IMemoryOwner<T> memoryOwner in this.memoryOwners!)
                {
                    memoryOwner.Dispose();
                }
            }

            this.memoryOwners = null;
            this.IsValid = false;
            this.groupLifetimeGuard = null;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        [MemberNotNull(nameof(memoryOwners))]
        private void EnsureNotDisposed()
        {
            if (this.memoryOwners is null)
            {
                ThrowObjectDisposedException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private static void ThrowObjectDisposedException() => throw new ObjectDisposedException(nameof(MemoryGroup<T>));

        // When the MemoryGroup points to multiple buffers via `groupLifetimeGuard`,
        // the lifetime of the individual buffers is managed by the guard.
        // Group buffer IMemoryOwner<T>-s d not manage ownership.
        private sealed class ObservedBuffer : MemoryManager<T>
        {
            private readonly UnmanagedMemoryHandle handle;
            private readonly int lengthInElements;

            private ObservedBuffer(UnmanagedMemoryHandle handle, int lengthInElements)
            {
                this.handle = handle;
                this.lengthInElements = lengthInElements;
            }

            public static ObservedBuffer Create(
                UnmanagedMemoryHandle handle,
                int lengthInElements,
                AllocationOptions options)
            {
                ObservedBuffer? buffer = new(handle, lengthInElements);
                if (options.Has(AllocationOptions.Clean))
                {
                    buffer.GetSpan().Clear();
                }

                return buffer;
            }

            protected override void Dispose(bool disposing)
            {
                // No-op.
            }

            public override unsafe Span<T> GetSpan() => new(this.handle.Pointer, this.lengthInElements);

            public override unsafe MemoryHandle Pin(int elementIndex = 0)
            {
                void* pbData = Unsafe.Add<T>(this.handle.Pointer, elementIndex);
                return new(pbData);
            }

            public override void Unpin()
            {
            }
        }
    }
}
