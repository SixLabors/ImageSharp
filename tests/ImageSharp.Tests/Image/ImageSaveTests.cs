// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Tests the <see cref="image"/> class.
/// </summary>
public class ImageSaveTests : IDisposable
{
    private readonly Image<Rgba32> image;
    private readonly Mock<IFileSystem> fileSystem;
    private readonly Mock<IImageEncoder> encoder;
    private readonly Mock<IImageEncoder> encoderNotInFormat;
    private IImageFormatDetector localMimeTypeDetector;
    private Mock<IImageFormat> localImageFormat;

    public ImageSaveTests()
    {
        this.localImageFormat = new();
        this.localImageFormat.Setup(x => x.FileExtensions).Returns(["png"]);
        this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormat.Object);

        this.encoder = new();

        this.encoderNotInFormat = new();

        this.fileSystem = new();
        Configuration config = new()
        {
            FileSystem = this.fileSystem.Object
        };
        config.ImageFormatsManager.AddImageFormatDetector(this.localMimeTypeDetector);
        config.ImageFormatsManager.SetEncoder(this.localImageFormat.Object, this.encoder.Object);
        this.image = new(config, 1, 1);
    }

    [Fact]
    public void SavePath()
    {
        using MemoryStream stream = new();
        this.fileSystem.Setup(x => x.Create("path.png")).Returns(stream);
        this.image.Save("path.png");

        this.encoder.Verify(x => x.Encode(this.image, stream));
    }

    [Fact]
    public void SavePathWithEncoder()
    {
        using MemoryStream stream = new();
        this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

        this.image.Save("path.jpg", this.encoderNotInFormat.Object);

        this.encoderNotInFormat.Verify(x => x.Encode(this.image, stream));
    }

    [Fact]
    public void ToBase64String()
    {
        string str = this.image.ToBase64String(this.localImageFormat.Object);

        this.encoder.Verify(x => x.Encode(this.image, It.IsAny<Stream>()));
    }

    [Fact]
    public void SaveStreamWithMime()
    {
        using MemoryStream stream = new();
        this.image.Save(stream, this.localImageFormat.Object);

        this.encoder.Verify(x => x.Encode(this.image, stream));
    }

    [Fact]
    public void SaveStreamWithEncoder()
    {
        using MemoryStream stream = new();

        this.image.Save(stream, this.encoderNotInFormat.Object);

        this.encoderNotInFormat.Verify(x => x.Encode(this.image, stream));
    }

    public void Dispose()
    {
        this.image.Dispose();
    }
}
