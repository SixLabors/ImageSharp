// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class AffineTransformTests
{
    private readonly ITestOutputHelper output;
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.033F, 3);

    /// <summary>
    /// angleDeg, sx, sy, tx, ty
    /// </summary>
    public static readonly TheoryData<float, float, float, float, float> TransformValues
        = new()
        {
                  { 0, 1, 1, 0, 0 },
                  { 50, 1, 1, 0, 0 },
                  { 0, 1, 1, 20, 10 },
                  { 50, 1, 1, 20, 10 },
                  { 0, 1, 1, -20, -10 },
                  { 50, 1, 1, -20, -10 },
                  { 50, 1.5f, 1.5f, 0, 0 },
                  { 50, 1.1F, 1.3F, 30, -20 },
                  { 0, 2f, 1f, 0, 0 },
                  { 0, 1f, 2f, 0, 0 },
              };

    public static readonly TheoryData<string> ResamplerNames = new()
    {
        nameof(KnownResamplers.Bicubic),
        nameof(KnownResamplers.Box),
        nameof(KnownResamplers.CatmullRom),
        nameof(KnownResamplers.Hermite),
        nameof(KnownResamplers.Lanczos2),
        nameof(KnownResamplers.Lanczos3),
        nameof(KnownResamplers.Lanczos5),
        nameof(KnownResamplers.Lanczos8),
        nameof(KnownResamplers.MitchellNetravali),
        nameof(KnownResamplers.NearestNeighbor),
        nameof(KnownResamplers.Robidoux),
        nameof(KnownResamplers.RobidouxSharp),
        nameof(KnownResamplers.Spline),
        nameof(KnownResamplers.Triangle),
        nameof(KnownResamplers.Welch),
    };

    public static readonly TheoryData<string> Transform_DoesNotCreateEdgeArtifacts_ResamplerNames =
        new()
        {
                nameof(KnownResamplers.NearestNeighbor),
                nameof(KnownResamplers.Triangle),
                nameof(KnownResamplers.Bicubic),
                nameof(KnownResamplers.Lanczos8),
            };

    public AffineTransformTests(ITestOutputHelper output) => this.output = output;

    /// <summary>
    /// The output of an "all white" image should be "all white" or transparent, regardless of the transformation and the resampler.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
    [Theory]
    [WithSolidFilledImages(nameof(Transform_DoesNotCreateEdgeArtifacts_ResamplerNames), 5, 5, 255, 255, 255, 255, PixelTypes.Rgba32)]
    public void Transform_DoesNotCreateEdgeArtifacts<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IResampler resampler = GetResampler(resamplerName);
        using Image<TPixel> image = provider.GetImage();
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(30);

        image.Mutate(c => c.Transform(builder, resampler));
        image.DebugSave(provider, resamplerName);

        VerifyAllPixelsAreWhiteOrTransparent(image);
    }

    [Theory]
    [WithTestPatternImages(nameof(TransformValues), 100, 50, PixelTypes.Rgba32)]
    public void Transform_RotateScaleTranslate<TPixel>(
        TestImageProvider<TPixel> provider,
        float angleDeg,
        float sx,
        float sy,
        float tx,
        float ty)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider, $"_original");
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(angleDeg)
            .AppendScale(new SizeF(sx, sy))
            .AppendTranslation(new PointF(tx, ty));

        this.PrintMatrix(builder.BuildMatrix(image.Size));

        image.Mutate(i => i.Transform(builder, KnownResamplers.Bicubic));

        FormattableString testOutputDetails = $"R({angleDeg})_S({sx},{sy})_T({tx},{ty})";
        image.DebugSave(provider, testOutputDetails);
        image.CompareToReferenceOutput(ValidatorComparer, provider, testOutputDetails);
    }

    [Theory]
    [WithTestPatternImages(96, 96, PixelTypes.Rgba32, 50, 0.8f)]
    public void Transform_RotateScale_ManuallyCentered<TPixel>(TestImageProvider<TPixel> provider, float angleDeg, float s)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(angleDeg)
            .AppendScale(new SizeF(s, s));

        image.Mutate(i => i.Transform(builder, KnownResamplers.Bicubic));

        FormattableString testOutputDetails = $"R({angleDeg})_S({s})";
        image.DebugSave(provider, testOutputDetails);
        image.CompareToReferenceOutput(ValidatorComparer, provider, testOutputDetails);
    }

    public static readonly TheoryData<int, int, int, int> Transform_IntoRectangle_Data =
        new()
        {
                { 0, 0, 10, 10 },
                { 0, 0, 5, 10 },
                { 0, 0, 10, 5 },
                { 5, 0, 5, 10 },
                { -5, -5, 20, 20 }
            };

    /// <summary>
    /// Testing transforms using custom source rectangles:
    /// https://github.com/SixLabors/ImageSharp/pull/386#issuecomment-357104963
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
    [Theory]
    [WithTestPatternImages(96, 48, PixelTypes.Rgba32)]
    public void Transform_FromSourceRectangle1<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle rectangle = new(48, 0, 48, 24);

        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider, $"_original");
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendScale(new SizeF(2, 1.5F));

        image.Mutate(i => i.Transform(rectangle, builder, KnownResamplers.Spline));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider);
    }

    [Theory]
    [WithTestPatternImages(96, 48, PixelTypes.Rgba32)]
    public void Transform_FromSourceRectangle2<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle rectangle = new(0, 24, 48, 24);

        using Image<TPixel> image = provider.GetImage();
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendScale(new SizeF(1F, 2F));

        image.Mutate(i => i.Transform(rectangle, builder, KnownResamplers.Spline));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider);
    }

    [Theory]
    [WithTestPatternImages(nameof(ResamplerNames), 150, 150, PixelTypes.Rgba32)]
    public void Transform_WithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IResampler sampler = GetResampler(resamplerName);
        using Image<TPixel> image = provider.GetImage();
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(50)
            .AppendScale(new SizeF(.6F, .6F));

        image.Mutate(i => i.Transform(builder, sampler));

        image.DebugSave(provider, resamplerName);
        image.CompareToReferenceOutput(ValidatorComparer, provider, resamplerName);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 21)]
    public void WorksWithDiscoBuffers<TPixel>(TestImageProvider<TPixel> provider, int bufferCapacityInPixelRows)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(50)
            .AppendScale(new SizeF(.6F, .6F));
        provider.RunBufferCapacityLimitProcessorTest(
            bufferCapacityInPixelRows,
            c => c.Transform(builder));
    }

    [Fact]
    public void Issue1911()
    {
        using Image<Rgba32> image = new(100, 100);
        image.Mutate(x => x = x.Transform(new Rectangle(0, 0, 99, 100), Matrix3x2.Identity, new Size(99, 100), KnownResamplers.Lanczos2));

        Assert.Equal(99, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Theory]
    [WithSolidFilledImages(4, 4, nameof(Color.Red), PixelTypes.Rgba32)]
    public void Issue2753<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        AffineTransformBuilder builder =
            new AffineTransformBuilder().AppendRotationDegrees(270, new Vector2(3.5f, 3.5f));
        image.Mutate(x => x.BackgroundColor(Color.Red));
        image.Mutate(x => x = x.Transform(builder));

        image.DebugSave(provider);

        Assert.Equal(4, image.Width);
        Assert.Equal(7, image.Height);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue3000, PixelTypes.Rgba32, 3, 3)]
    [WithFile(TestImages.Png.Issue3000, PixelTypes.Rgba32, 4, 4)]
    public void Issue3000<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Mutate(c => c
        .Transform(new AffineTransformBuilder().AppendRotationDegrees(90, new Vector2(x, y))));

        string details = $"p-{x}-{y}";
        image.DebugSave(provider, testOutputDetails: details);
        image.CompareToReferenceOutput(ValidatorComparer, provider, testOutputDetails: details);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void Identity<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        Matrix3x2 m = Matrix3x2.Identity;
        Rectangle r = new(25, 25, 50, 50);
        image.Mutate(x => x.Transform(r, m, new Size(100, 100), KnownResamplers.Bicubic));
        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 0.0001F)]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 57F)]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 0F)]
    public void Transform_With_Custom_Dimensions<TPixel>(TestImageProvider<TPixel> provider, float radians)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        Matrix3x2 m = Matrix3x2.CreateRotation(radians, new Vector2(50, 50));
        Rectangle r = new(25, 25, 50, 50);
        image.Mutate(x => x.Transform(r, m, new Size(100, 100), KnownResamplers.Bicubic));
        image.DebugSave(provider, testOutputDetails: radians);
        image.CompareToReferenceOutput(ValidatorComparer, provider, testOutputDetails: radians);
    }

    [Theory]
    [WithSolidFilledImages(100, 100, "DimGray", PixelTypes.Rgba32)]
    public void TransformRotationDoesNotOffset<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rgba32 background = Color.DimGray.ToPixel<Rgba32>();
        TPixel marker = Color.Aqua.ToPixel<TPixel>();

        using Image<TPixel> canvas = provider.GetImage();

        using Image<TPixel> img = canvas.Clone();
        img[0, 0] = marker;

        img.Mutate(c => c.Rotate(180));

        Assert.Equal(marker, img[99, 99]);

        img.DebugSave(provider, "Rotate180");

        using Image<TPixel> img2 = canvas.Clone();
        img2[0, 0] = marker;

        img2.Mutate(
            c =>
            c.Transform(new AffineTransformBuilder().AppendRotationDegrees(180), KnownResamplers.NearestNeighbor));

        img.DebugSave(provider, "AffineRotate180NN");

        using Image<TPixel> img3 = canvas.Clone();
        img3[0, 0] = marker;

        img3.Mutate(c => c.Transform(new AffineTransformBuilder().AppendRotationDegrees(180)));

        img3.DebugSave(provider, "AffineRotate180Bicubic");

        ImageComparer.Exact.VerifySimilarity(img, img2);
        ImageComparer.Exact.VerifySimilarity(img, img3);
    }

    private static IResampler GetResampler(string name)
    {
        PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name)
                                ?? throw new InvalidOperationException($"No resampler named {name}");

        return (IResampler)property.GetValue(null);
    }

    private static void VerifyAllPixelsAreWhiteOrTransparent<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Assert.True(image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<TPixel> data));
        Rgb24 white = new(255, 255, 255);
        foreach (TPixel pixel in data.Span)
        {
            Rgba32 rgba = pixel.ToRgba32();
            if (rgba.A == 0)
            {
                continue;
            }

            Assert.Equal(white, rgba.Rgb);
        }
    }

    private void PrintMatrix(Matrix3x2 a)
    {
        string s = $"{a.M11:F10},{a.M12:F10},{a.M21:F10},{a.M22:F10},{a.M31:F10},{a.M32:F10}";
        this.output.WriteLine(s);
    }
}
