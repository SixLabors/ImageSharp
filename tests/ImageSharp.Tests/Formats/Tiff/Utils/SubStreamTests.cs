// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.IO;
    using ImageSharp.Formats.Tiff;
    using Xunit;

    [Trait("Category", "Tiff")]
    public class SubStreamTests
    {
        [Fact]
        public void Constructor_PositionsStreamCorrectly_WithSpecifiedOffset()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            innerStream.Position = 2;

            SubStream stream = new SubStream(innerStream, 4, 6);

            Assert.Equal(0, stream.Position);
            Assert.Equal(6, stream.Length);
            Assert.Equal(4, innerStream.Position);
        }

        [Fact]
        public void Constructor_PositionsStreamCorrectly_WithCurrentOffset()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            innerStream.Position = 2;

            SubStream stream = new SubStream(innerStream, 6);

            Assert.Equal(0, stream.Position);
            Assert.Equal(6, stream.Length);
            Assert.Equal(2, innerStream.Position);
        }

        [Fact]
        public void CanRead_ReturnsTrue()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void CanWrite_ReturnsFalse()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.False(stream.CanWrite);
        }

        [Fact]
        public void CanSeek_ReturnsTrue()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.True(stream.CanSeek);
        }

        [Fact]
        public void Length_ReturnsTheConstrainedLength()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Equal(6, stream.Length);
        }

        [Fact]
        public void Position_ReturnsZeroBeforeReading()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Equal(0, stream.Position);
            Assert.Equal(2, innerStream.Position);
        }

        [Fact]
        public void Position_ReturnsPositionAfterReading()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Read(new byte[2], 0, 2);

            Assert.Equal(2, stream.Position);
            Assert.Equal(4, innerStream.Position);
        }

        [Fact]
        public void Position_ReturnsPositionAfterReadingTwice()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Read(new byte[2], 0, 2);
            stream.Read(new byte[2], 0, 2);

            Assert.Equal(4, stream.Position);
            Assert.Equal(6, innerStream.Position);
        }

        [Fact]
        public void Position_SettingPropertySeeksToNewPosition()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 3;

            Assert.Equal(3, stream.Position);
            Assert.Equal(5, innerStream.Position);
        }

        [Fact]
        public void Flush_ThrowsNotSupportedException()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Throws<NotSupportedException>(() => stream.Flush());
        }

        [Fact]
        public void Read_Reads_FromStartOfSubStream()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            byte[] buffer = new byte[3];
            var result = stream.Read(buffer, 0, 3);

            Assert.Equal(new byte[] { 3, 4, 5 }, buffer);
            Assert.Equal(3, result);
        }

        [Theory]
        [InlineData(2, SeekOrigin.Begin)]
        [InlineData(1, SeekOrigin.Current)]
        [InlineData(4, SeekOrigin.End)]
        public void Read_Reads_FromMiddleOfSubStream(long offset, SeekOrigin origin)
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            stream.Seek(offset, origin);
            byte[] buffer = new byte[3];
            var result = stream.Read(buffer, 0, 3);

            Assert.Equal(new byte[] { 5, 6, 7 }, buffer);
            Assert.Equal(3, result);
        }

        [Theory]
        [InlineData(3, SeekOrigin.Begin)]
        [InlineData(2, SeekOrigin.Current)]
        [InlineData(3, SeekOrigin.End)]
        public void Read_Reads_FromEndOfSubStream(long offset, SeekOrigin origin)
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            stream.Seek(offset, origin);
            byte[] buffer = new byte[3];
            var result = stream.Read(buffer, 0, 3);

            Assert.Equal(new byte[] { 6, 7, 8 }, buffer);
            Assert.Equal(3, result);
        }

        [Theory]
        [InlineData(4, SeekOrigin.Begin)]
        [InlineData(3, SeekOrigin.Current)]
        [InlineData(2, SeekOrigin.End)]
        public void Read_Reads_FromBeyondEndOfSubStream(long offset, SeekOrigin origin)
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            stream.Seek(offset, origin);
            byte[] buffer = new byte[3];
            var result = stream.Read(buffer, 0, 3);

            Assert.Equal(new byte[] { 7, 8, 0 }, buffer);
            Assert.Equal(2, result);
        }

        [Fact]
        public void ReadByte_Reads_FromStartOfSubStream()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            var result = stream.ReadByte();

            Assert.Equal(3, result);
        }

        [Fact]
        public void ReadByte_Reads_FromMiddleOfSubStream()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 3;
            var result = stream.ReadByte();

            Assert.Equal(6, result);
        }

        [Fact]
        public void ReadByte_Reads_FromEndOfSubStream()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 5;
            var result = stream.ReadByte();

            Assert.Equal(8, result);
        }

        [Fact]
        public void ReadByte_Reads_FromBeyondEndOfSubStream()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 5;
            stream.ReadByte();
            var result = stream.ReadByte();

            Assert.Equal(-1, result);
        }

        [Fact]
        public void Write_ThrowsNotSupportedException()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Throws<NotSupportedException>(() => stream.Write(new byte[] { 1, 2 }, 0, 2));
        }

        [Fact]
        public void WriteByte_ThrowsNotSupportedException()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
        }

        [Fact]
        public void Seek_MovesToNewPosition_FromBegin()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            long result = stream.Seek(2, SeekOrigin.Begin);

            Assert.Equal(2, result);
            Assert.Equal(2, stream.Position);
            Assert.Equal(4, innerStream.Position);
        }

        [Fact]
        public void Seek_MovesToNewPosition_FromCurrent()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            long result = stream.Seek(2, SeekOrigin.Current);

            Assert.Equal(3, result);
            Assert.Equal(3, stream.Position);
            Assert.Equal(5, innerStream.Position);
        }

        [Fact]
        public void Seek_MovesToNewPosition_FromEnd()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            stream.Position = 1;
            long result = stream.Seek(2, SeekOrigin.End);

            Assert.Equal(4, result);
            Assert.Equal(4, stream.Position);
            Assert.Equal(6, innerStream.Position);
        }

        [Fact]
        public void Seek_ThrowsException_WithInvalidOrigin()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            var e = Assert.Throws<ArgumentException>(() => stream.Seek(2, (SeekOrigin)99));
            Assert.Equal("Invalid seek origin.", e.Message);
        }

        [Fact]
        public void SetLength_ThrowsNotSupportedException()
        {
            Stream innerStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            SubStream stream = new SubStream(innerStream, 2, 6);

            Assert.Throws<NotSupportedException>(() => stream.SetLength(5));
        }
    }
}
