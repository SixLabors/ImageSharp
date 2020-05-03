// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a byte buffer backed by a managed array. Useful for interop with classic .NET API-s.
    /// </summary>
    public interface IManagedByteBuffer : IMemoryOwner<byte>
    {
        /// <summary>
        /// Gets the managed array backing this buffer instance.
        /// </summary>
        byte[] Array { get; }
    }
}