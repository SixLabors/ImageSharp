// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BitStream.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   A stream for reading bits in a sequence of bytes.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// A stream for reading bits in a sequence of bytes.
    /// </summary>
    internal class BitStream : IDisposable
    {
        /// <summary>
        /// The number of bits in byte.
        /// </summary>
        private const int BitsInByte = 8;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// The underlying stream.
        /// </summary>
        private Stream stream;

        /// <summary>
        /// The current position.
        /// </summary>
        private int currentPosition;

        /// <summary>
        /// The size of the underlying stream.
        /// </summary>
        private int size;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitStream"/> class.
        /// </summary>
        public BitStream()
        {
            this.stream = new MemoryStream();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BitStream"/> class based on the 
        /// specified byte array.
        /// </summary>
        /// <param name="buffer">
        /// The <see cref="T:byte[]"/> from which to create the current stream.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given buffer is null.
        /// </exception>
        public BitStream(byte[] buffer)
        {
            Guard.NotNull(buffer, "buffer");

            this.stream = new MemoryStream(buffer);
            this.size = this.BitsAllocated();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="BitStream"/> class. 
        /// </summary>
        ~BitStream()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(true);
        }

        /// <summary>
        /// Gets the underlying stream.
        /// </summary>
        public Stream UnderlyingStream => this.stream;

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the size of the stream.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/> representing the size.
        /// </returns>
        public int Size()
        {
            return this.size;
        }

        /// <summary>
        /// Returns a number representing the given number of bits read from the stream
        /// advancing the stream by that number.
        /// </summary>
        /// <param name="bitCount">The number of bits to read.</param>
        /// <returns>
        /// The <see cref="int"/> representing the total number of bits read.
        /// </returns>
        public virtual int Read(int bitCount)
        {
            Guard.LessEquals(this.Tell() + bitCount, this.BitsAllocated(), "bitCount");
            return this.ReadBits(bitCount);
        }

        /// <summary>
        /// Writes a block of bits represented by an <see cref="int"/> to the current stream.
        /// </summary>
        /// <param name="bitStorage">The bits to write.</param>
        /// <param name="bitCount">The number of bits to write.</param>
        /// <returns>
        /// The <see cref="int"/> representing the number of bits written.
        /// </returns>
        public int Write(int bitStorage, int bitCount)
        {
            if (bitCount == 0)
            {
                return 0;
            }

            const int MaxBitsInStorage = sizeof(int) * BitsInByte;

            Guard.LessEquals(bitCount, MaxBitsInStorage, "bitCount");

            for (int i = 0; i < bitCount; ++i)
            {
                byte bit = (byte)((bitStorage << (MaxBitsInStorage - (bitCount - i))) >> (MaxBitsInStorage - 1));
                if (!this.WriteBits(bit))
                {
                    return i;
                }
            }

            return bitCount;
        }

        /// <summary>
        /// Sets the position within the current stream to the specified value.
        /// </summary>
        /// <param name="position">
        /// The new position within the stream. 
        /// This is relative to the <paramref name="location"/> parameter, and can be positive or negative.
        /// </param>
        /// <param name="location">
        /// A value of type <see cref="SeekOrigin"/>, which acts as the seek reference point.
        /// </param>
        public void Seek(int position, SeekOrigin location)
        {
            switch (location)
            {
                case SeekOrigin.Begin:
                    this.SeekSet(position);
                    break;

                case SeekOrigin.Current:
                    this.SeekCurrent(position);
                    break;

                case SeekOrigin.End:
                    this.SeekSet(this.Size() + position);
                    break;
            }
        }

        /// <summary>
        /// TODO: Document this.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int Tell()
        {
            return ((int)this.stream.Position * BitsInByte) + this.currentPosition;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.stream != null)
                {
                    this.stream.Dispose();
                    this.stream = null;
                }
            }

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Returns the number of bits allocated to the stream.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/> representing the number of bits.
        /// </returns>
        private int BitsAllocated()
        {
            return (int)this.stream.Length * BitsInByte;
        }

        /// <summary>
        /// Returns a number representing the given number of bits read from the stream.
        /// </summary>
        /// <param name="bitsCount">The number of bits to read.</param>
        /// <returns>
        /// The <see cref="int"/> representing the total number of bits read.
        /// </returns>
        private int ReadBits(int bitsCount)
        {
            // Codes are packed into a continuous bit stream, high-order bit first. 
            // This stream is then divided into 8-bit bytes, high-order bit first. 
            // Thus, codes can straddle byte boundaries arbitrarily. After the EOD marker (code value 257), 
            // any leftover bits in the final byte are set to 0.
            Guard.BetweenEquals(bitsCount, 0, 32, "bitsCount");

            if (bitsCount == 0)
            {
                return 0;
            }

            int bitsRead = 0;
            int result = 0;
            byte[] bt = new byte[1];
            while (bitsRead == 0 || (bitsRead - this.currentPosition < bitsCount))
            {
                this.stream.Read(bt, 0, 1);

                result = result << BitsInByte;
                result += bt[0];

                bitsRead += 8;
            }

            this.currentPosition = (this.currentPosition + bitsCount) % 8;
            if (this.currentPosition != 0)
            {
                result = result >> (BitsInByte - this.currentPosition);

                this.stream.Seek(-1, SeekOrigin.Current);
            }

            if (bitsCount < 32)
            {
                int mask = (1 << bitsCount) - 1;
                result = result & mask;
            }

            return result;
        }

        /// <summary>
        /// Writes a block of bits represented to the current stream.
        /// </summary>
        /// <param name="bits">
        /// The bits to write.
        /// </param>
        /// <returns>
        /// True. TODO: investigate this as it always returns true.
        /// </returns>
        private bool WriteBits(byte bits)
        {
            if (this.stream.Position == this.stream.Length)
            {
                byte[] bytes = { (byte)(bits << (BitsInByte - 1)) };
                this.stream.Write(bytes, 0, 1);
                this.stream.Seek(-1, SeekOrigin.Current);
            }
            else
            {
                byte[] bytes = { 0 };
                this.stream.Read(bytes, 0, 1);
                this.stream.Seek(-1, SeekOrigin.Current);

                int shift = (BitsInByte - this.currentPosition - 1) % BitsInByte;
                byte maskByte = (byte)(bits << shift);

                bytes[0] |= maskByte;
                this.stream.Write(bytes, 0, 1);
                this.stream.Seek(-1, SeekOrigin.Current);
            }

            this.Seek(1, SeekOrigin.Current);

            int position = this.Tell();
            if (position > this.size)
            {
                this.size = position;
            }

            return true;
        }

        /// <summary>
        /// Sets the position within the current stream to the specified value.
        /// </summary>
        /// <param name="position">
        /// The new position within the stream. Can be positive or negative.
        /// </param>
        private void SeekSet(int position)
        {
            Guard.GreaterEquals(position, 0, "position");

            int byteDisplacement = position / BitsInByte;
            this.stream.Seek(byteDisplacement, SeekOrigin.Begin);

            int shiftInByte = position - (byteDisplacement * BitsInByte);
            this.currentPosition = shiftInByte;
        }

        /// <summary>
        /// Sets the position to current position in the current stream.
        /// </summary>
        /// <param name="position">
        /// The new position within the stream. Can be positive or negative.
        /// </param>
        private void SeekCurrent(int position)
        {
            int result = this.Tell() + position;
            Guard.BetweenEquals(position, 0, this.BitsAllocated(), "position");

            this.SeekSet(result);
        }
    }
}
