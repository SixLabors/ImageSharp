namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a byte buffer backed by a managed array.
    /// </summary>
    internal interface IManagedByteBuffer : IBuffer<byte>
    {
        /// <summary>
        /// Gets the managed array backing this buffer instance.
        /// </summary>
        byte[] Array { get; }
    }
}