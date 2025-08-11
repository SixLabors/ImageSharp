// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Formats;

public class GeneralFormatTests
{
    /// <summary>
    /// A collection made up of one file for each image format.
    /// </summary>
    public static readonly IEnumerable<string> DefaultFiles =
    [
        TestImages.Bmp.Car,
            TestImages.Jpeg.Baseline.Calliphora,
            TestImages.Png.Splash,
            TestImages.Gif.Trans
    ];

    /// <summary>
    /// The collection of image files to test against.
    /// </summary>
    protected static readonly List<TestFile> Files =
    [
        TestFile.Create(TestImages.Jpeg.Baseline.Calliphora),
        TestFile.Create(TestImages.Bmp.Car),
        TestFile.Create(TestImages.Png.Splash),
        TestFile.Create(TestImages.Gif.Rings)
    ];

    [Theory]
    [WithFileCollection(nameof(DefaultFiles), PixelTypes.Rgba32)]
    public void ResolutionShouldChange<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Metadata.VerticalResolution = 150;
        image.Metadata.HorizontalResolution = 150;
        image.DebugSave(provider);
    }

    [Fact]
    public void ChainedReadOriginIsRespectedForSeekableStreamsOnLoad()
    {
        using FileStream stream = File.OpenRead(TestFile.GetInputFileFullPath(TestImages.Png.Issue2259));
        using Image<Rgb24> i = Image.Load<Rgb24>(stream);
        long position1 = stream.Position;
        Assert.NotEqual(0, position1);

        using Image<Rgb24> j = Image.Load<Rgb24>(stream);
        long position2 = stream.Position;
        Assert.True(position2 > position1);

        Assert.NotEqual(i[5, 5], j[5, 5]);
    }

    [Fact]
    public void ChainedReadOnLoadNonSeekable_ThrowsUnknownImageFormatException()
    {
        using FileStream stream = File.OpenRead(TestFile.GetInputFileFullPath(TestImages.Png.Issue2259));
        using NonSeekableStream wrapper = new(stream);
        using Image<Rgb24> i = Image.Load<Rgb24>(wrapper);

        Assert.Equal(stream.Length, stream.Position);
        Assert.Throws<UnknownImageFormatException>(() => { using Image<Rgb24> j = Image.Load<Rgb24>(wrapper); });
    }

    [Fact]
    public async Task ChainedReadOriginIsRespectedForSeekableStreamsOnLoadAsync()
    {
        using FileStream stream = File.OpenRead(TestFile.GetInputFileFullPath(TestImages.Png.Issue2259));
        using Image<Rgb24> i = await Image.LoadAsync<Rgb24>(stream);
        long position1 = stream.Position;
        Assert.NotEqual(0, position1);

        using Image<Rgb24> j = await Image.LoadAsync<Rgb24>(stream);
        long position2 = stream.Position;
        Assert.True(position2 > position1);

        Assert.NotEqual(i[5, 5], j[5, 5]);
    }

    [Fact]
    public async Task ChainedReadOnLoadNonSeekable_ThrowsUnknownImageFormatException_Async()
    {
        using FileStream stream = File.OpenRead(TestFile.GetInputFileFullPath(TestImages.Png.Issue2259));
        using NonSeekableStream wrapper = new(stream);
        using Image<Rgb24> i = await Image.LoadAsync<Rgb24>(wrapper);

        Assert.Equal(stream.Length, stream.Position);
        await Assert.ThrowsAsync<UnknownImageFormatException>(async () => { using Image<Rgb24> j = await Image.LoadAsync<Rgb24>(wrapper); });
    }

    [Fact]
    public void ImageCanEncodeToString()
    {
        string path = TestEnvironment.CreateOutputDirectory("ToString");

        foreach (TestFile file in Files)
        {
            using Image<Rgba32> image = file.CreateRgba32Image();
            string filename = Path.Combine(path, $"{file.FileNameWithoutExtension}.txt");
            File.WriteAllText(filename, image.ToBase64String(PngFormat.Instance));
        }
    }

    [Fact]
    public void DecodeThenEncodeImageFromStreamShouldSucceed()
    {
        string path = TestEnvironment.CreateOutputDirectory("Encode");

        foreach (TestFile file in Files)
        {
            using Image<Rgba32> image = file.CreateRgba32Image();
            image.Save(Path.Combine(path, file.FileName));
        }
    }

    public static readonly TheoryData<string> QuantizerNames =
        new()
        {
            nameof(KnownQuantizers.Octree),
            nameof(KnownQuantizers.WebSafe),
            nameof(KnownQuantizers.Werner),
            nameof(KnownQuantizers.Wu)
        };

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, nameof(QuantizerNames), PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bike, nameof(QuantizerNames), PixelTypes.Rgba32)]
    public void QuantizeImageShouldPreserveMaximumColorPrecision<TPixel>(TestImageProvider<TPixel> provider, string quantizerName)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IQuantizer quantizer = GetQuantizer(quantizerName);

        using (Image<TPixel> image = provider.GetImage())
        {
            image.DebugSave(provider, new PngEncoder { ColorType = PngColorType.Palette, Quantizer = quantizer }, testOutputDetails: quantizerName);
        }

        provider.Configuration.MemoryAllocator.ReleaseRetainedResources();
    }

    private static IQuantizer GetQuantizer(string name)
    {
        PropertyInfo property = typeof(KnownQuantizers).GetTypeInfo().GetProperty(name);
        return (IQuantizer)property.GetMethod.Invoke(null, []);
    }

    [Fact]
    public void ImageCanConvertFormat()
    {
        string path = TestEnvironment.CreateOutputDirectory("Format");

        foreach (TestFile file in Files)
        {
            using Image<Rgba32> image = file.CreateRgba32Image();
            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.bmp")))
            {
                image.SaveAsBmp(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.jpg")))
            {
                image.SaveAsJpeg(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.pbm")))
            {
                image.SaveAsPbm(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.png")))
            {
                image.SaveAsPng(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.gif")))
            {
                image.SaveAsGif(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.tga")))
            {
                image.SaveAsTga(output);
            }

            using (FileStream output = File.Create(Path.Combine(path, $"{file.FileNameWithoutExtension}.tiff")))
            {
                image.SaveAsTiff(output);
            }
        }
    }

    [Fact]
    public void ImageShouldPreservePixelByteOrderWhenSerialized()
    {
        string path = TestEnvironment.CreateOutputDirectory("Serialized");

        foreach (TestFile file in Files)
        {
            byte[] serialized;
            using (Image image = Image.Load(file.Bytes))
            using (MemoryStream memoryStream = new())
            {
                image.Save(memoryStream, image.Metadata.DecodedImageFormat);
                memoryStream.Flush();
                serialized = memoryStream.ToArray();
            }

            using Image<Rgba32> image2 = Image.Load<Rgba32>(serialized);
            image2.Save($"{path}{Path.DirectorySeparatorChar}{file.FileName}");
        }
    }

    [Theory]
    [InlineData(10, 10, "pbm")]
    [InlineData(100, 100, "pbm")]
    [InlineData(100, 10, "pbm")]
    [InlineData(10, 100, "pbm")]
    [InlineData(10, 10, "png")]
    [InlineData(100, 100, "png")]
    [InlineData(100, 10, "png")]
    [InlineData(10, 100, "png")]
    [InlineData(10, 10, "gif")]
    [InlineData(100, 100, "gif")]
    [InlineData(100, 10, "gif")]
    [InlineData(10, 100, "gif")]
    [InlineData(10, 10, "bmp")]
    [InlineData(100, 100, "bmp")]
    [InlineData(100, 10, "bmp")]
    [InlineData(10, 100, "bmp")]
    [InlineData(10, 10, "jpg")]
    [InlineData(100, 100, "jpg")]
    [InlineData(100, 10, "jpg")]
    [InlineData(10, 100, "jpg")]
    [InlineData(100, 100, "tga")]
    [InlineData(100, 10, "tga")]
    [InlineData(10, 100, "tga")]
    [InlineData(100, 100, "tiff")]
    [InlineData(100, 10, "tiff")]
    [InlineData(10, 100, "tiff")]

    public void CanIdentifyImageLoadedFromBytes(int width, int height, string extension)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(new Rgba32[width * height], width, height);
        using MemoryStream memoryStream = new();
        IImageFormat format = GetFormat(extension);
        image.Save(memoryStream, format);
        memoryStream.Position = 0;

        ImageInfo imageInfo = Image.Identify(memoryStream);

        Assert.Equal(imageInfo.Width, width);
        Assert.Equal(imageInfo.Height, height);
        Assert.Equal(format, imageInfo.Metadata.DecodedImageFormat);
    }

    [Fact]
    public void Identify_UnknownImageFormatException_WithInvalidStream()
    {
        byte[] invalid = new byte[10];

        using MemoryStream memoryStream = new(invalid);

        Assert.Throws<UnknownImageFormatException>(() => Image.Identify(invalid));
    }

    private static IImageFormat GetFormat(string format)
        => Configuration.Default.ImageFormats
        .FirstOrDefault(x => x.FileExtensions.Contains(format));
}
