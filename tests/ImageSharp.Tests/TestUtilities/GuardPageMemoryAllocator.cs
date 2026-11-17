// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory;

/// <summary>
/// Allocates each buffer so that its last byte is directly before a page that the process cannot access.
/// A read or a write past the end of a buffer stops the process with an access violation.
/// </summary>
/// <remarks>
/// The access violation cannot be caught, so run code that uses this allocator in a child process
/// (for example with <c>RemoteExecutor</c>). A crash of the child process then fails only that test.
/// </remarks>
internal sealed unsafe class GuardPageMemoryAllocator : MemoryAllocator
{
    /// <inheritdoc />
    protected internal override int GetBufferCapacityInBytes() => int.MaxValue;

    /// <inheritdoc />
    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        => new GuardPageBuffer<T>(length);

    /// <summary>
    /// Owns one memory region: the data pages, then one guard page with no access.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private sealed class GuardPageBuffer<T> : MemoryManager<T>
        where T : struct
    {
        private readonly int length;
        private readonly byte* region;
        private readonly nuint regionLength;
        private readonly byte* data;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuardPageBuffer{T}"/> class.
        /// </summary>
        /// <param name="length">The number of elements in the buffer.</param>
        public GuardPageBuffer(int length)
        {
            nuint pageSize = (nuint)Environment.SystemPageSize;
            nuint byteCount = (nuint)length * (nuint)Unsafe.SizeOf<T>();
            nuint dataPageCount = (byteCount + pageSize - 1) / pageSize;

            this.length = length;
            this.regionLength = (dataPageCount + 1) * pageSize;
            this.region = VirtualMemory.AllocateReadWrite(this.regionLength);

            byte* guardPage = this.region + (dataPageCount * pageSize);
            VirtualMemory.ProtectNoAccess(guardPage, pageSize);

            // The data ends exactly at the guard page, so the first byte past the buffer is not accessible.
            // The OS gives zeroed pages, so the buffer is clean for all allocation options.
            this.data = guardPage - byteCount;
        }

        /// <inheritdoc />
        public override Span<T> GetSpan() => new(this.data, this.length);

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0)
            => new(this.data + ((nuint)elementIndex * (nuint)Unsafe.SizeOf<T>()), default, this);

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) => VirtualMemory.Free(this.region, this.regionLength);
    }

    /// <summary>
    /// Calls the page allocation functions of the operating system.
    /// </summary>
    private static class VirtualMemory
    {
        // Windows values from winnt.h.
        // MEM_COMMIT, MEM_RESERVE: https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-virtualalloc
        // MEM_RELEASE: https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-virtualfree
        // PAGE_NOACCESS, PAGE_READWRITE: https://learn.microsoft.com/windows/win32/memory/memory-protection-constants
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint PageNoAccess = 0x01;
        private const uint PageReadWrite = 0x04;

        // Unix values from sys/mman.h. PROT_NONE, PROT_READ, PROT_WRITE and MAP_PRIVATE are the same on Linux and macOS.
        // MAP_ANONYMOUS is not: Linux uses 0x20 and macOS uses 0x1000 (MAP_ANON).
        // Linux: https://github.com/torvalds/linux/blob/master/include/uapi/asm-generic/mman-common.h
        // and https://github.com/torvalds/linux/blob/master/include/uapi/linux/mman.h
        // macOS: https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/mman.h
        private const int ProtNone = 0;
        private const int ProtRead = 1;
        private const int ProtWrite = 2;
        private const int MapPrivate = 0x02;
        private const int MapAnonymousLinux = 0x20;
        private const int MapAnonymousMacOS = 0x1000;

        /// <summary>
        /// Allocates readable and writable pages.
        /// </summary>
        /// <param name="length">The number of bytes. This is a multiple of the page size.</param>
        /// <returns>The address of the first page.</returns>
        public static byte* AllocateReadWrite(nuint length)
        {
            if (OperatingSystem.IsWindows())
            {
                void* address = VirtualAlloc(null, length, MemCommit | MemReserve, PageReadWrite);
                return address is not null ? (byte*)address : throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            int anonymous = OperatingSystem.IsMacOS() ? MapAnonymousMacOS : MapAnonymousLinux;

            // An anonymous mapping has no file, so the file descriptor is -1 and the offset is 0.
            // On failure, mmap returns MAP_FAILED, which is (void*)-1, not null.
            void* mapped = Mmap(null, length, ProtRead | ProtWrite, MapPrivate | anonymous, -1, 0);
            return mapped != (void*)-1 ? (byte*)mapped : throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        /// <summary>
        /// Removes all access to the pages.
        /// </summary>
        /// <param name="address">The address of the first page.</param>
        /// <param name="length">The number of bytes. This is a multiple of the page size.</param>
        public static void ProtectNoAccess(byte* address, nuint length)
        {
            bool success;
            if (OperatingSystem.IsWindows())
            {
                uint oldProtect;
                success = VirtualProtect(address, length, PageNoAccess, &oldProtect) != 0;
            }
            else
            {
                success = Mprotect(address, length, ProtNone) == 0;
            }

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        /// <summary>
        /// Releases pages that <see cref="AllocateReadWrite(nuint)"/> allocated.
        /// </summary>
        /// <param name="address">The address of the first page.</param>
        /// <param name="length">The number of bytes given to <see cref="AllocateReadWrite(nuint)"/>.</param>
        public static void Free(byte* address, nuint length)
        {
            if (OperatingSystem.IsWindows())
            {
                // MEM_RELEASE requires a size of 0. It then releases the full region that VirtualAlloc reserved.
                _ = VirtualFree(address, 0, MemRelease);
            }
            else
            {
                _ = Munmap(address, length);
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern void* VirtualAlloc(void* address, nuint size, uint allocationType, uint protect);

        [DllImport("kernel32", SetLastError = true)]
        private static extern int VirtualProtect(void* address, nuint size, uint newProtect, uint* oldProtect);

        [DllImport("kernel32", SetLastError = true)]
        private static extern int VirtualFree(void* address, nuint size, uint freeType);

        [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
        private static extern void* Mmap(void* address, nuint length, int protection, int flags, int fileDescriptor, nint offset);

        [DllImport("libc", EntryPoint = "mprotect", SetLastError = true)]
        private static extern int Mprotect(void* address, nuint length, int protection);

        [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
        private static extern int Munmap(void* address, nuint length);
    }
}
