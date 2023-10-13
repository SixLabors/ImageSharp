// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests;

public class EofHitCountingReadStreamTests : IDisposable
{
    private const int Length = 1000;
    private readonly EofHitCountingReadStream stream;

    public EofHitCountingReadStreamTests()
    {
        MemoryStream ms = new(new byte[Length]);
        ms.Seek(0, SeekOrigin.Begin);
        this.stream = new(ms);
    }

    public void Dispose() => this.stream.Dispose();

    [Fact]
    public void ReadByte()
    {
        for (int i = 0; i < Length; i++)
        {
            this.stream.ReadByte();
            Assert.Equal(0, this.stream.EofHitCount);
        }

        this.stream.ReadByte();
        this.stream.ReadByte();
        this.stream.ReadByte();
        Assert.Equal(3, this.stream.EofHitCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadSync(bool arrayApi)
    {
        byte[] buffer = new byte[Length / 10];

        for (int i = 0; i < 10; i++)
        {
            Read();
            Assert.Equal(0, this.stream.EofHitCount);
        }

        Read();
        Read();
        Read();

        Assert.Equal(3, this.stream.EofHitCount);

        void Read()
        {
            if (arrayApi)
            {
                this.stream.Read(buffer, 0, buffer.Length);
            }
            else
            {
                this.stream.Read(buffer.AsSpan());
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync(bool arrayApi)
    {
        byte[] buffer = new byte[Length / 10];

        for (int i = 0; i < 10; i++)
        {
            await ReadAsync();
            Assert.Equal(0, this.stream.EofHitCount);
        }

        await ReadAsync();
        await ReadAsync();
        await ReadAsync();

        Assert.Equal(3, this.stream.EofHitCount);

        async ValueTask ReadAsync()
        {
            if (arrayApi)
            {
                await this.stream.ReadAsync(buffer, 0, buffer.Length);
            }
            else
            {
                await this.stream.ReadAsync(buffer.AsMemory());
            }
        }
    }
}
