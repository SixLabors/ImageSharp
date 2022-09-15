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
        string testData = Guid.NewGuid().ToString();
        File.WriteAllText(path, testData);

        var fs = new LocalFileSystem();

        using (var r = new StreamReader(fs.OpenRead(path)))
        {
            string data = r.ReadToEnd();

            Assert.Equal(testData, data);
        }

        File.Delete(path);
    }

    [Fact]
    public void Create()
    {
        string path = Path.GetTempFileName();
        string testData = Guid.NewGuid().ToString();
        var fs = new LocalFileSystem();

        using (var r = new StreamWriter(fs.Create(path)))
        {
            r.Write(testData);
        }

        string data = File.ReadAllText(path);
        Assert.Equal(testData, data);

        File.Delete(path);
    }
}
