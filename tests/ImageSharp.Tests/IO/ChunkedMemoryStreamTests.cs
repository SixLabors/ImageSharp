// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.IO;

/// <summary>
/// Tests for the <see cref="ChunkedMemoryStream"/> class.
/// </summary>
public class ChunkedMemoryStreamTests
{
    private readonly Random bufferFiller = new();

    /// <summary>
    /// The default length in bytes of each buffer chunk when allocating large buffers.
    /// </summary>
    private const int DefaultLargeChunkSize = 1024 * 1024 * 4; // 4 Mb

    /// <summary>
    /// The default length in bytes of each buffer chunk when allocating small buffers.
    /// </summary>
    private const int DefaultSmallChunkSize = DefaultLargeChunkSize / 32; // 128 Kb

    private readonly MemoryAllocator allocator;

    public ChunkedMemoryStreamTests() => this.allocator = Configuration.Default.MemoryAllocator;

    [Fact]
    public void MemoryStream_GetPositionTest_Negative()
    {
        using ChunkedMemoryStream ms = new(this.allocator);
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
        ChunkedMemoryStream ms2 = new(this.allocator);

        Assert.Throws<ArgumentNullException>(() => ms2.Read(null, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ms2.Read(new byte[] { 1 }, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ms2.Read(new byte[] { 1 }, 0, -1));
        Assert.Throws<ArgumentException>(() => ms2.Read(new byte[] { 1 }, 2, 0));
        Assert.Throws<ArgumentException>(() => ms2.Read(new byte[] { 1 }, 0, 2));

        ms2.Dispose();

        Assert.Throws<ObjectDisposedException>(() => ms2.Read(new byte[] { 1 }, 0, 1));
    }

    [Theory]
    [InlineData(DefaultSmallChunkSize)]
    [InlineData((int)(DefaultSmallChunkSize * 1.5))]
    [InlineData(DefaultSmallChunkSize * 4)]
    [InlineData((int)(DefaultSmallChunkSize * 5.5))]
    [InlineData(DefaultSmallChunkSize * 16)]
    public void MemoryStream_ReadByteTest(int length)
    {
        using MemoryStream ms = this.CreateTestStream(length);
        using ChunkedMemoryStream cms = new(this.allocator);

        ms.CopyTo(cms);
        cms.Position = 0;
        byte[] expected = ms.ToArray();

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], cms.ReadByte());
        }
    }

    [Theory]
    [InlineData(DefaultSmallChunkSize)]
    [InlineData((int)(DefaultSmallChunkSize * 1.5))]
    [InlineData(DefaultSmallChunkSize * 4)]
    [InlineData((int)(DefaultSmallChunkSize * 5.5))]
    [InlineData(DefaultSmallChunkSize * 16)]
    public void MemoryStream_ReadByteBufferTest(int length)
    {
        using MemoryStream ms = this.CreateTestStream(length);
        using ChunkedMemoryStream cms = new(this.allocator);

        ms.CopyTo(cms);
        cms.Position = 0;
        byte[] expected = ms.ToArray();
        byte[] buffer = new byte[2];
        for (int i = 0; i < expected.Length; i += 2)
        {
            cms.Read(buffer);
            Assert.Equal(expected[i], buffer[0]);
            Assert.Equal(expected[i + 1], buffer[1]);
        }
    }

    [Theory]
    [InlineData(DefaultSmallChunkSize)]
    [InlineData((int)(DefaultSmallChunkSize * 1.5))]
    [InlineData(DefaultSmallChunkSize * 4)]
    [InlineData((int)(DefaultSmallChunkSize * 5.5))]
    [InlineData(DefaultSmallChunkSize * 16)]
    [InlineData(DefaultSmallChunkSize * 32)]
    public void MemoryStream_ReadByteBufferSpanTest(int length)
    {
        using MemoryStream ms = this.CreateTestStream(length);
        using ChunkedMemoryStream cms = new(this.allocator);

        ms.CopyTo(cms);
        cms.Position = 0;
        byte[] expected = ms.ToArray();
        Span<byte> buffer = new byte[2];
        for (int i = 0; i < expected.Length; i += 2)
        {
            cms.Read(buffer);
            Assert.Equal(expected[i], buffer[0]);
            Assert.Equal(expected[i + 1], buffer[1]);
        }
    }

    [Theory]
    [InlineData(DefaultSmallChunkSize)]
    [InlineData((int)(DefaultSmallChunkSize * 1.5))]
    [InlineData(DefaultSmallChunkSize * 4)]
    [InlineData((int)(DefaultSmallChunkSize * 5.5))]
    [InlineData(DefaultSmallChunkSize * 16)]
    [InlineData(DefaultSmallChunkSize * 32)]
    public void MemoryStream_WriteToTests(int length)
    {
        using (ChunkedMemoryStream ms2 = new(this.allocator))
        {
            byte[] bytArrRet;
            byte[] bytArr = this.CreateTestBuffer(length);

            // [] Write to memoryStream, check the memoryStream
            ms2.Write(bytArr, 0, bytArr.Length);

            using ChunkedMemoryStream readonlyStream = new(this.allocator);
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
        using (ChunkedMemoryStream ms2 = new(this.allocator))
        using (ChunkedMemoryStream ms3 = new(this.allocator))
        {
            byte[] bytArrRet;
            byte[] bytArr = this.CreateTestBuffer(length);

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

    [Theory]
    [InlineData(DefaultSmallChunkSize)]
    [InlineData((int)(DefaultSmallChunkSize * 1.5))]
    [InlineData(DefaultSmallChunkSize * 4)]
    [InlineData((int)(DefaultSmallChunkSize * 5.5))]
    [InlineData(DefaultSmallChunkSize * 16)]
    [InlineData(DefaultSmallChunkSize * 32)]
    public void MemoryStream_WriteToSpanTests(int length)
    {
        using (ChunkedMemoryStream ms2 = new(this.allocator))
        {
            Span<byte> bytArrRet;
            Span<byte> bytArr = this.CreateTestBuffer(length);

            // [] Write to memoryStream, check the memoryStream
            ms2.Write(bytArr, 0, bytArr.Length);

            using ChunkedMemoryStream readonlyStream = new(this.allocator);
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
        using (ChunkedMemoryStream ms2 = new(this.allocator))
        using (ChunkedMemoryStream ms3 = new(this.allocator))
        {
            Span<byte> bytArrRet;
            Span<byte> bytArr = this.CreateTestBuffer(length);

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
        using ChunkedMemoryStream ms2 = new(this.allocator);
        byte[] bytArrRet;
        byte[] bytArr = new byte[] { byte.MinValue, byte.MaxValue, 1, 2, 3, 4, 5, 6, 128, 250 };

        for (int i = 0; i < bytArr.Length; i++)
        {
            ms2.WriteByte(bytArr[i]);
        }

        using ChunkedMemoryStream readonlyStream = new(this.allocator);
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

    [Fact]
    public void MemoryStream_WriteToTests_Negative()
    {
        using ChunkedMemoryStream ms2 = new(this.allocator);
        Assert.Throws<ArgumentNullException>(() => ms2.WriteTo(null));

        ms2.Write(new byte[] { 1 }, 0, 1);
        MemoryStream readonlyStream = new(new byte[1028], false);
        Assert.Throws<NotSupportedException>(() => ms2.WriteTo(readonlyStream));

        readonlyStream.Dispose();

        // [] Pass in a closed stream
        Assert.Throws<ObjectDisposedException>(() => ms2.WriteTo(readonlyStream));
    }

    [Fact]
    public void MemoryStream_CopyTo_Invalid()
    {
        ChunkedMemoryStream memoryStream;
        const string bufferSize = nameof(bufferSize);
        using (memoryStream = new ChunkedMemoryStream(this.allocator))
        {
            const string destination = nameof(destination);
            Assert.Throws<ArgumentNullException>(destination, () => memoryStream.CopyTo(destination: null));

            // Validate the destination parameter first.
            Assert.Throws<ArgumentNullException>(destination, () => memoryStream.CopyTo(destination: null, bufferSize: 0));
            Assert.Throws<ArgumentNullException>(destination, () => memoryStream.CopyTo(destination: null, bufferSize: -1));

            // Then bufferSize.
            Assert.Throws<ArgumentOutOfRangeException>(bufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: 0)); // 0-length buffer doesn't make sense.
            Assert.Throws<ArgumentOutOfRangeException>(bufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: -1));
        }

        // After the Stream is disposed, we should fail on all CopyTos.
        Assert.Throws<ArgumentOutOfRangeException>(bufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: 0)); // Not before bufferSize is validated.
        Assert.Throws<ArgumentOutOfRangeException>(bufferSize, () => memoryStream.CopyTo(Stream.Null, bufferSize: -1));

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
        using ChunkedMemoryStream destination = new(this.allocator);
        source.CopyTo(destination);
        Assert.InRange(source.Position, source.Length, int.MaxValue); // Copying the data should have read to the end of the stream or stayed past the end.
        Assert.Equal(expected, destination.ToArray());
    }

    public static IEnumerable<string> GetAllTestImages()
    {
        IEnumerable<string> allImageFiles = Directory.EnumerateFiles(TestEnvironment.InputImagesDirectoryFullPath, "*.*", SearchOption.AllDirectories)
            .Where(s => !s.EndsWith("txt", StringComparison.OrdinalIgnoreCase));

        List<string> result = new();
        foreach (string path in allImageFiles)
        {
            result.Add(path[TestEnvironment.InputImagesDirectoryFullPath.Length..]);
        }

        return result;
    }

    public static IEnumerable<string> AllTestImages { get; } = GetAllTestImages();

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
        using NonSeekableStream nonSeekableStream = new(fs);

        using Image<TPixel> actual = Image.Load<TPixel>(nonSeekableStream);

        ImageComparer.Exact.VerifySimilarity(expected, actual);
        expected.Dispose();
    }

    [Theory]
    [WithFileCollection(nameof(AllTestImages), PixelTypes.Rgba32)]
    public void EncoderIntegrationTest<TPixel>(TestImageProvider<TPixel> provider)
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

        using MemoryStream ms = new();
        using NonSeekableStream nonSeekableStream = new(ms);
        expected.SaveAsWebp(nonSeekableStream);

        using Image<TPixel> actual = Image.Load<TPixel>(nonSeekableStream);

        ImageComparer.Exact.VerifySimilarity(expected, actual);
        expected.Dispose();
    }

    public static IEnumerable<object[]> CopyToData()
    {
        // Stream is positioned @ beginning of data
        byte[] data1 = new byte[] { 1, 2, 3 };
        MemoryStream stream1 = new(data1);

        yield return new object[] { stream1, data1 };

        // Stream is positioned in the middle of data
        byte[] data2 = new byte[] { 0xff, 0xf3, 0xf0 };
        MemoryStream stream2 = new(data2) { Position = 1 };

        yield return new object[] { stream2, new byte[] { 0xf3, 0xf0 } };

        // Stream is positioned after end of data
        byte[] data3 = data2;
        MemoryStream stream3 = new(data3) { Position = data3.Length + 1 };

        yield return new object[] { stream3, Array.Empty<byte>() };
    }

    private byte[] CreateTestBuffer(int length)
    {
        byte[] buffer = new byte[length];
        this.bufferFiller.NextBytes(buffer);
        return buffer;
    }

    private MemoryStream CreateTestStream(int length)
        => new(this.CreateTestBuffer(length));
}
