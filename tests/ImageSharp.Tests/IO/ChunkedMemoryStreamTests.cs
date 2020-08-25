// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    /// <summary>
    /// Tests for the <see cref="ChunkedMemoryStream"/> class.
    /// </summary>
    public class ChunkedMemoryStreamTests
    {
        private readonly MemoryAllocator allocator;

        public ChunkedMemoryStreamTests()
        {
            this.allocator = Configuration.Default.MemoryAllocator;
        }

        [Fact]
        public void MemoryStream_Ctor_InvalidCapacities()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkedMemoryStream(int.MinValue, this.allocator));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkedMemoryStream(0, this.allocator));
        }

        [Fact]
        public void MemoryStream_GetPositionTest_Negative()
        {
            using var ms = new ChunkedMemoryStream(this.allocator);
            long iCurrentPos = ms.Position;
            for (int i = -1; i > -6; i--)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => ms.Position = i);
                Assert.Equal(ms.Position, iCurrentPos);
            }
        }

        [Fact]
        public void MemoryStream_ReadTest_Negative()
        {
            var ms2 = new ChunkedMemoryStream(this.allocator);

            Assert.Throws<ArgumentNullException>(() => ms2.Read(null, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ms2.Read(new byte[] { 1 }, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ms2.Read(new byte[] { 1 }, 0, -1));
            Assert.Throws<ArgumentException>(() => ms2.Read(new byte[] { 1 }, 2, 0));
            Assert.Throws<ArgumentException>(() => ms2.Read(new byte[] { 1 }, 0, 2));

            ms2.Dispose();

            Assert.Throws<ObjectDisposedException>(() => ms2.Read(new byte[] { 1 }, 0, 1));
        }

        [Theory]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 1.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 4)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 5.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 8)]
        public void MemoryStream_ReadByteTest(int length)
        {
            using MemoryStream ms = this.CreateTestStream(length);
            using var cms = new ChunkedMemoryStream(this.allocator);

            ms.CopyTo(cms);
            cms.Position = 0;
            var expected = ms.ToArray();

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], cms.ReadByte());
            }
        }

        [Theory]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 1.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 4)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 5.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 8)]
        public void MemoryStream_ReadByteBufferTest(int length)
        {
            using MemoryStream ms = this.CreateTestStream(length);
            using var cms = new ChunkedMemoryStream(this.allocator);

            ms.CopyTo(cms);
            cms.Position = 0;
            var expected = ms.ToArray();
            var buffer = new byte[2];
            for (int i = 0; i < expected.Length; i += 2)
            {
                cms.Read(buffer);
                Assert.Equal(expected[i], buffer[0]);
                Assert.Equal(expected[i + 1], buffer[1]);
            }
        }

        [Theory]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 1.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 4)]
        [InlineData((int)(ChunkedMemoryStream.DefaultBufferLength * 5.5))]
        [InlineData(ChunkedMemoryStream.DefaultBufferLength * 8)]
        public void MemoryStream_ReadByteBufferSpanTest(int length)
        {
            using MemoryStream ms = this.CreateTestStream(length);
            using var cms = new ChunkedMemoryStream(this.allocator);

            ms.CopyTo(cms);
            cms.Position = 0;
            var expected = ms.ToArray();
            Span<byte> buffer = new byte[2];
            for (int i = 0; i < expected.Length; i += 2)
            {
                cms.Read(buffer);
                Assert.Equal(expected[i], buffer[0]);
                Assert.Equal(expected[i + 1], buffer[1]);
            }
        }

        [Fact]
        public void MemoryStream_WriteToTests()
        {
            using (var ms2 = new ChunkedMemoryStream(this.allocator))
            {
                byte[] bytArrRet;
                byte[] bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

                // [] Write to memoryStream, check the memoryStream
                ms2.Write(bytArr, 0, bytArr.Length);

                using var readonlyStream = new ChunkedMemoryStream(this.allocator);
                ms2.WriteTo(readonlyStream);
                readonlyStream.Flush();
                readonlyStream.Position = 0;
                bytArrRet = new byte[(int)readonlyStream.Length];
                readonlyStream.Read(bytArrRet, 0, (int)readonlyStream.Length);
                for (int i = 0; i < bytArr.Length; i++)
                {
                    Assert.Equal(bytArr[i], bytArrRet[i]);
                }
            }

            // [] Write to memoryStream, check the memoryStream
            using (var ms2 = new ChunkedMemoryStream(this.allocator))
            using (var ms3 = new ChunkedMemoryStream(this.allocator))
            {
                byte[] bytArrRet;
                byte[] bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

                ms2.Write(bytArr, 0, bytArr.Length);
                ms2.WriteTo(ms3);
                ms3.Position = 0;
                bytArrRet = new byte[(int)ms3.Length];
                ms3.Read(bytArrRet, 0, (int)ms3.Length);
                for (int i = 0; i < bytArr.Length; i++)
                {
                    Assert.Equal(bytArr[i], bytArrRet[i]);
                }
            }
        }

        [Fact]
        public void MemoryStream_WriteToSpanTests()
        {
            using (var ms2 = new ChunkedMemoryStream(this.allocator))
            {
                Span<byte> bytArrRet;
                Span<byte> bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

                // [] Write to memoryStream, check the memoryStream
                ms2.Write(bytArr, 0, bytArr.Length);

                using var readonlyStream = new ChunkedMemoryStream(this.allocator);
                ms2.WriteTo(readonlyStream);
                readonlyStream.Flush();
                readonlyStream.Position = 0;
                bytArrRet = new byte[(int)readonlyStream.Length];
                readonlyStream.Read(bytArrRet, 0, (int)readonlyStream.Length);
                for (int i = 0; i < bytArr.Length; i++)
                {
                    Assert.Equal(bytArr[i], bytArrRet[i]);
                }
            }

            // [] Write to memoryStream, check the memoryStream
            using (var ms2 = new ChunkedMemoryStream(this.allocator))
            using (var ms3 = new ChunkedMemoryStream(this.allocator))
            {
                Span<byte> bytArrRet;
                Span<byte> bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

                ms2.Write(bytArr, 0, bytArr.Length);
                ms2.WriteTo(ms3);
                ms3.Position = 0;
                bytArrRet = new byte[(int)ms3.Length];
                ms3.Read(bytArrRet, 0, (int)ms3.Length);
                for (int i = 0; i < bytArr.Length; i++)
                {
                    Assert.Equal(bytArr[i], bytArrRet[i]);
                }
            }
        }

        [Fact]
        public void MemoryStream_WriteByteTests()
        {
            using (var ms2 = new ChunkedMemoryStream(this.allocator))
            {
                byte[] bytArrRet;
                byte[] bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

                for (int i = 0; i < bytArr.Length; i++)
                {
                    ms2.WriteByte(bytArr[i]);
                }

                using var readonlyStream = new ChunkedMemoryStream(this.allocator);
                ms2.WriteTo(readonlyStream);
                readonlyStream.Flush();
                readonlyStream.Position = 0;
                bytArrRet = new byte[(int)readonlyStream.Length];
                readonlyStream.Read(bytArrRet, 0, (int)readonlyStream.Length);
                for (int i = 0; i < bytArr.Length; i++)
                {
                    Assert.Equal(bytArr[i], bytArrRet[i]);
                }
            }
        }

        [Fact]
        public void MemoryStream_WriteToTests_Negative()
        {
            using var ms2 = new ChunkedMemoryStream(this.allocator);
            Assert.Throws<ArgumentNullException>(() => ms2.WriteTo(null));

            ms2.Write(new byte[] { 1 }, 0, 1);
            var readonlyStream = new MemoryStream(new byte[1028], false);
            Assert.Throws<NotSupportedException>(() => ms2.WriteTo(readonlyStream));

            readonlyStream.Dispose();

            // [] Pass in a closed stream
            Assert.Throws<ObjectDisposedException>(() => ms2.WriteTo(readonlyStream));
        }

        [Fact]
        public void MemoryStream_CopyTo_Invalid()
        {
            ChunkedMemoryStream memoryStream;
            const string BufferSize = "bufferSize";
            using (memoryStream = new ChunkedMemoryStream(this.allocator))
            {
                const string Destination = "destination";
                Assert.Throws<ArgumentNullException>(Destination, () => memoryStream.CopyTo(destination: null));

                // Validate the destination parameter first.
                Assert.Throws<ArgumentNullException>(Destination, () => memoryStream.CopyTo(destination: null, bufferSize: 0));
                Assert.Throws<ArgumentNullException>(Destination, () => memoryStream.CopyTo(destination: null, bufferSize: -1));

                // Then bufferSize.
                Assert.Throws<ArgumentOutOfRangeException>(BufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: 0)); // 0-length buffer doesn't make sense.
                Assert.Throws<ArgumentOutOfRangeException>(BufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: -1));
            }

            // After the Stream is disposed, we should fail on all CopyTos.
            Assert.Throws<ArgumentOutOfRangeException>(BufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: 0)); // Not before bufferSize is validated.
            Assert.Throws<ArgumentOutOfRangeException>(BufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: -1));

            ChunkedMemoryStream disposedStream = memoryStream;

            // We should throw first for the source being disposed...
            Assert.Throws<ObjectDisposedException>(() => memoryStream.CopyTo(disposedStream, 1));

            // Then for the destination being disposed.
            memoryStream = new ChunkedMemoryStream(this.allocator);
            Assert.Throws<ObjectDisposedException>(() => memoryStream.CopyTo(disposedStream, 1));
            memoryStream.Dispose();
        }

        [Theory]
        [MemberData(nameof(CopyToData))]
        public void CopyTo(Stream source, byte[] expected)
        {
            using var destination = new ChunkedMemoryStream(this.allocator);
            source.CopyTo(destination);
            Assert.InRange(source.Position, source.Length, int.MaxValue); // Copying the data should have read to the end of the stream or stayed past the end.
            Assert.Equal(expected, destination.ToArray());
        }

        public static IEnumerable<string> GetAllTestImages()
        {
            IEnumerable<string> allImageFiles = Directory.EnumerateFiles(TestEnvironment.InputImagesDirectoryFullPath, "*.*", SearchOption.AllDirectories)
                .Where(s => !s.EndsWith("txt", StringComparison.OrdinalIgnoreCase));

            var result = new List<string>();
            foreach (string path in allImageFiles)
            {
                result.Add(path.Substring(TestEnvironment.InputImagesDirectoryFullPath.Length));
            }

            return result;
        }

        public static IEnumerable<string> AllTestImages = GetAllTestImages();

        [Theory]
        [WithFileCollection(nameof(AllTestImages), PixelTypes.Rgba32)]
        public void DecoderIntegrationTest<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                return;
            }

            Image<TPixel> expected;
            try
            {
                expected = provider.GetImage();
            }
            catch
            {
                // The image is invalid
                return;
            }

            string fullPath = Path.Combine(
                TestEnvironment.InputImagesDirectoryFullPath,
                ((TestImageProvider<TPixel>.FileProvider)provider).FilePath);

            using FileStream fs = File.OpenRead(fullPath);
            using var nonSeekableStream = new NonSeekableStream(fs);

            var actual = Image.Load<TPixel>(nonSeekableStream);

            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }

        public static IEnumerable<object[]> CopyToData()
        {
            // Stream is positioned @ beginning of data
            byte[] data1 = new byte[] { 1, 2, 3 };
            var stream1 = new MemoryStream(data1);

            yield return new object[] { stream1, data1 };

            // Stream is positioned in the middle of data
            byte[] data2 = new byte[] { 0xff, 0xf3, 0xf0 };
            var stream2 = new MemoryStream(data2) { Position = 1 };

            yield return new object[] { stream2, new byte[] { 0xf3, 0xf0 } };

            // Stream is positioned after end of data
            byte[] data3 = data2;
            var stream3 = new MemoryStream(data3) { Position = data3.Length + 1 };

            yield return new object[] { stream3, Array.Empty<byte>() };
        }

        private MemoryStream CreateTestStream(int length)
        {
            var buffer = new byte[length];
            var random = new Random();
            random.NextBytes(buffer);

            return new MemoryStream(buffer);
        }
    }
}
