// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public class TestEnvironmentTests
{
    public TestEnvironmentTests(ITestOutputHelper output)
        => this.Output = output;

    private ITestOutputHelper Output { get; }

    private void CheckPath(string path)
    {
        this.Output.WriteLine(path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SolutionDirectoryFullPath()
        => this.CheckPath(TestEnvironment.SolutionDirectoryFullPath);

    [Fact]
    public void InputImagesDirectoryFullPath()
        => this.CheckPath(TestEnvironment.InputImagesDirectoryFullPath);

    [Fact]
    public void ExpectedOutputDirectoryFullPath()
        => this.CheckPath(TestEnvironment.ReferenceOutputDirectoryFullPath);

    [Fact]
    public void GetReferenceOutputFileName()
    {
        string actual = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, @"foo\bar\lol.jpeg");
        string expected = TestEnvironment.GetReferenceOutputFileName(actual);

        this.Output.WriteLine(expected);
        Assert.Contains(TestEnvironment.ReferenceOutputDirectoryFullPath, expected);
    }

    [Theory]
    [InlineData("lol/foo.png", typeof(SystemDrawingReferenceEncoder))]
    [InlineData("lol/Rofl.bmp", typeof(SystemDrawingReferenceEncoder))]
    [InlineData("lol/Baz.JPG", typeof(JpegEncoder))]
    [InlineData("lol/Baz.gif", typeof(GifEncoder))]
    [InlineData("lol/foobar.webp", typeof(WebpEncoder))]
    public void GetReferenceEncoder_ReturnsCorrectEncoders_Windows(string fileName, Type expectedEncoderType)
    {
        if (!TestEnvironment.IsWindows)
        {
            return;
        }

        IImageEncoder encoder = TestEnvironment.GetReferenceEncoder(fileName);
        Assert.IsType(expectedEncoderType, encoder);
    }

    [Theory]
    [InlineData("lol/foo.png", typeof(MagickReferenceDecoder))]
    [InlineData("lol/Rofl.bmp", typeof(SystemDrawingReferenceDecoder))]
    [InlineData("lol/Baz.JPG", typeof(JpegDecoder))]
    [InlineData("lol/Baz.gif", typeof(GifDecoder))]
    [InlineData("lol/foobar.webp", typeof(WebpDecoder))]
    public void GetReferenceDecoder_ReturnsCorrectDecoders_Windows(string fileName, Type expectedDecoderType)
    {
        if (!TestEnvironment.IsWindows)
        {
            return;
        }

        ImageDecoder decoder = TestEnvironment.GetReferenceDecoder(fileName);
        Assert.IsType(expectedDecoderType, decoder);
    }

    [Theory]
    [InlineData("lol/foo.png", typeof(ImageSharpPngEncoderWithDefaultConfiguration))]
    [InlineData("lol/Rofl.bmp", typeof(BmpEncoder))]
    [InlineData("lol/Baz.JPG", typeof(JpegEncoder))]
    [InlineData("lol/Baz.gif", typeof(GifEncoder))]
    [InlineData("lol/foobar.webp", typeof(WebpEncoder))]
    public void GetReferenceEncoder_ReturnsCorrectEncoders_Linux(string fileName, Type expectedEncoderType)
    {
        if (!TestEnvironment.IsLinux)
        {
            return;
        }

        IImageEncoder encoder = TestEnvironment.GetReferenceEncoder(fileName);
        Assert.IsType(expectedEncoderType, encoder);
    }

    [Theory]
    [InlineData("lol/foo.png", typeof(MagickReferenceDecoder))]
    [InlineData("lol/Rofl.bmp", typeof(MagickReferenceDecoder))]
    [InlineData("lol/Baz.JPG", typeof(JpegDecoder))]
    [InlineData("lol/Baz.gif", typeof(GifDecoder))]
    [InlineData("lol/foobar.webp", typeof(WebpDecoder))]
    public void GetReferenceDecoder_ReturnsCorrectDecoders_Linux(string fileName, Type expectedDecoderType)
    {
        if (!TestEnvironment.IsLinux)
        {
            return;
        }

        ImageDecoder decoder = TestEnvironment.GetReferenceDecoder(fileName);
        Assert.IsType(expectedDecoderType, decoder);
    }

    // RemoteExecutor does not work with "dotnet xunit" used to run tests on 32 bit .NET Framework:
    // https://github.com/SixLabors/ImageSharp/blob/381dff8640b721a34b1227c970fcf6ad6c5e3e72/ci-test.ps1#L30
    public static bool IsNot32BitNetFramework = !TestEnvironment.IsFramework || TestEnvironment.Is64BitProcess;

    [ConditionalFact(nameof(IsNot32BitNetFramework))]
    public void RemoteExecutor_FailingRemoteTestShouldFailLocalTest()
    {
        static void FailingCode()
        {
            Assert.False(true);
        }

        Assert.ThrowsAny<RemoteExecutionException>(() => RemoteExecutor.Invoke(FailingCode).Dispose());
    }
}
