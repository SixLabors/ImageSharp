// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.IO;

public class LocalFileSystemTests
{
    [Fact]
    public void OpenRead()
    {
        string path = Path.GetTempFileName();
        try
        {
            string testData = Guid.NewGuid().ToString();
            File.WriteAllText(path, testData);

            LocalFileSystem fs = new();

            using (FileStream stream = (FileStream)fs.OpenRead(path))
            using (StreamReader reader = new(stream))
            {
                Assert.False(stream.IsAsync);
                Assert.True(stream.CanRead);
                Assert.False(stream.CanWrite);

                string data = reader.ReadToEnd();

                Assert.Equal(testData, data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenReadAsynchronous()
    {
        string path = Path.GetTempFileName();
        try
        {
            string testData = Guid.NewGuid().ToString();
            File.WriteAllText(path, testData);

            LocalFileSystem fs = new();

            await using (FileStream stream = (FileStream)fs.OpenReadAsynchronous(path))
            using (StreamReader reader = new(stream))
            {
                Assert.True(stream.IsAsync);
                Assert.True(stream.CanRead);
                Assert.False(stream.CanWrite);

                string data = await reader.ReadToEndAsync();

                Assert.Equal(testData, data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Create()
    {
        string path = Path.GetTempFileName();
        try
        {
            string testData = Guid.NewGuid().ToString();
            LocalFileSystem fs = new();

            using (FileStream stream = (FileStream)fs.Create(path))
            using (StreamWriter writer = new(stream))
            {
                Assert.False(stream.IsAsync);
                Assert.True(stream.CanRead);
                Assert.True(stream.CanWrite);

                writer.Write(testData);
            }

            string data = File.ReadAllText(path);
            Assert.Equal(testData, data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CreateAsynchronous()
    {
        string path = Path.GetTempFileName();
        try
        {
            string testData = Guid.NewGuid().ToString();
            LocalFileSystem fs = new();

            await using (FileStream stream = (FileStream)fs.CreateAsynchronous(path))
            await using (StreamWriter writer = new(stream))
            {
                Assert.True(stream.IsAsync);
                Assert.True(stream.CanRead);
                Assert.True(stream.CanWrite);

                await writer.WriteAsync(testData);
            }

            string data = File.ReadAllText(path);
            Assert.Equal(testData, data);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
