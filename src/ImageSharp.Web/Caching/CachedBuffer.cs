namespace ImageSharp.Web.Caching
{
    using System;
    using System.Linq;

    /// <summary>
    /// Encapulates the cached image buffer and length.
    /// </summary>
    public struct CachedBuffer : IEquatable<CachedBuffer>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedBuffer"/> struct.
        /// </summary>
        /// <param name="buffer">The cached image buffer</param>
        /// <param name="length">The length, in bytes, of the cached image</param>
        public CachedBuffer(byte[] buffer, long length)
        {
            this.Buffer = buffer;
            this.Length = length;
        }

        /// <summary>
        /// Gets the buffer
        /// </summary>
        public byte[] Buffer { get; }

        /// <summary>
        /// Gets the buffer length. This can differ from the buffer legth if the buffer has been rented.
        /// </summary>
        public long Length { get; }

        /// <inheritdoc/>
        public bool Equals(CachedBuffer other)
        {
            return this.Buffer.SequenceEqual(other.Buffer) && this.Length == other.Length;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CachedBuffer && this.Equals((CachedBuffer)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.Buffer != null ? this.Buffer.GetHashCode() : 0) * 397) ^ this.Length.GetHashCode();
            }
        }
    }
}