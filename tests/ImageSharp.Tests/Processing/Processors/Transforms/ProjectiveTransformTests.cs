// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Reflection;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
public class ProjectiveTransformTests
{
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.03f, 3);
    private static readonly ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.5f, 3);

    private ITestOutputHelper Output { get; }

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

    public static readonly TheoryData<TaperSide, TaperCorner> TaperMatrixData = new()
    {
        { TaperSide.Bottom, TaperCorner.Both },
        { TaperSide.Bottom, TaperCorner.LeftOrTop },
        { TaperSide.Bottom, TaperCorner.RightOrBottom },
        { TaperSide.Top, TaperCorner.Both },
        { TaperSide.Top, TaperCorner.LeftOrTop },
        { TaperSide.Top, TaperCorner.RightOrBottom },
        { TaperSide.Left, TaperCorner.Both },
        { TaperSide.Left, TaperCorner.LeftOrTop },
        { TaperSide.Left, TaperCorner.RightOrBottom },
        { TaperSide.Right, TaperCorner.Both },
        { TaperSide.Right, TaperCorner.LeftOrTop },
        { TaperSide.Right, TaperCorner.RightOrBottom },
    };

    public static readonly TheoryData<PointF, PointF, PointF, PointF> QuadDistortionData = new()
    {
        { new PointF(0, 0), new PointF(150, 0), new PointF(150, 150), new PointF(0, 150) }, // source == destination
        { new PointF(25, 50), new PointF(210, 25), new PointF(140, 210), new PointF(15, 125) }, // Distortion
        { new PointF(-50, -50), new PointF(200, -50), new PointF(200, 200), new PointF(-50, 200) }, // Scaling
        { new PointF(150, 0), new PointF(150, 150), new PointF(0, 150), new PointF(0, 0) }, // Rotation
    };

    public ProjectiveTransformTests(ITestOutputHelper output) => this.Output = output;

    [Theory]
    [WithTestPatternImages(nameof(ResamplerNames), 150, 150, PixelTypes.Rgba32)]
    public void Transform_WithSampler<TPixel>(TestImageProvider<TPixel> provider, string resamplerName)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IResampler sampler = GetResampler(resamplerName);
        using Image<TPixel> image = provider.GetImage();
        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendTaper(TaperSide.Right, TaperCorner.Both, .5F);

        image.Mutate(i => i.Transform(builder, sampler));

        image.DebugSave(provider, resamplerName);
        image.CompareToReferenceOutput(ValidatorComparer, provider, resamplerName);
    }

    [Theory]
    [WithSolidFilledImages(nameof(TaperMatrixData), 30, 30, nameof(Color.Red), PixelTypes.Rgba32)]
    public void Transform_WithTaperMatrix<TPixel>(TestImageProvider<TPixel> provider, TaperSide taperSide, TaperCorner taperCorner)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendTaper(taperSide, taperCorner, .5F);

        image.Mutate(i => i.Transform(builder));

        FormattableString testOutputDetails = $"{taperSide}-{taperCorner}";
        image.DebugSave(provider, testOutputDetails);
        image.CompareFirstFrameToReferenceOutput(TolerantComparer, provider, testOutputDetails);
    }

    [Theory]
    [WithTestPatternImages(nameof(QuadDistortionData), 150, 150, PixelTypes.Rgba32)]
    public void Transform_WithQuadDistortion<TPixel>(TestImageProvider<TPixel> provider, PointF topLeft, PointF topRight, PointF bottomRight, PointF bottomLeft)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendQuadDistortion(topLeft, topRight, bottomRight, bottomLeft);

        image.Mutate(i => i.Transform(builder));

        FormattableString testOutputDetails = $"{topLeft}-{topRight}-{bottomRight}-{bottomLeft}";
        image.DebugSave(provider, testOutputDetails);
        image.CompareFirstFrameToReferenceOutput(TolerantComparer, provider, testOutputDetails);
    }

    [Theory]
    [WithSolidFilledImages(100, 100, 0, 0, 255, PixelTypes.Rgba32)]
    public void RawTransformMatchesDocumentedExample<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Printing some extra output to help investigating rounding errors:
        this.Output.WriteLine($"Vector.IsHardwareAccelerated: {Vector.IsHardwareAccelerated}");

        // This test matches the output described in the example at
        // https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/transforms/non-affine
        using Image<TPixel> image = provider.GetImage();
        Matrix4x4 matrix = Matrix4x4.Identity;
        matrix.M14 = 0.01F;

        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendMatrix(matrix);

        image.Mutate(i => i.Transform(builder));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(TolerantComparer, provider);
    }

    [Theory]
    [WithSolidFilledImages(290, 154, 0, 0, 255, PixelTypes.Rgba32)]
    public void PerspectiveTransformMatchesCSS<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // https://jsfiddle.net/dFrHS/545/
        // https://github.com/SixLabors/ImageSharp/issues/787
        using Image<TPixel> image = provider.GetImage();
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
        Matrix4x4 matrix = new(
            0.260987f, -0.434909f, 0, -0.0022184f,
            0.373196f, 0.949882f, 0, -0.000312129f,
            0, 0, 1, 0,
            52, 165, 0, 1);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines

        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendMatrix(matrix);

        image.Mutate(i => i.Transform(builder));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(TolerantComparer, provider);
    }

    [Fact]
    public void Issue1911()
    {
        using Image<Rgba32> image = new(100, 100);
        image.Mutate(x => x = x.Transform(new Rectangle(0, 0, 99, 100), Matrix4x4.Identity, new Size(99, 100), KnownResamplers.Lanczos2));

        Assert.Equal(99, image.Width);
        Assert.Equal(100, image.Height);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void Identity<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        Matrix4x4 m = Matrix4x4.Identity;
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

        Matrix4x4 m = Matrix4x4.CreateRotationX(radians, new Vector3(50, 50, 1F)) * Matrix4x4.CreateRotationY(radians, new Vector3(50, 50, 1F));
        Rectangle r = new(25, 25, 50, 50);
        image.Mutate(x => x.Transform(r, m, new Size(100, 100), KnownResamplers.Bicubic));
        image.DebugSave(provider, testOutputDetails: radians);
        image.CompareToReferenceOutput(ValidatorComparer, provider, testOutputDetails: radians);
    }

    [Fact]
    public void TransformRotationDoesNotOffset()
    {
        Rgba32 background = Color.DimGray.ToPixel<Rgba32>();
        Rgba32 marker = Color.Aqua.ToPixel<Rgba32>();

        using Image<Rgba32> img = new(100, 100, background);
        img[0, 0] = marker;

        img.Mutate(c => c.Rotate(180));

        Assert.Equal(marker, img[99, 99]);

        using Image<Rgba32> img2 = new(100, 100, background);
        img2[0, 0] = marker;

        img2.Mutate(
            c =>
            c.Transform(new ProjectiveTransformBuilder().AppendRotationDegrees(180), KnownResamplers.NearestNeighbor));

        using Image<Rgba32> img3 = new(100, 100, background);
        img3[0, 0] = marker;

        img3.Mutate(c => c.Transform(new AffineTransformBuilder().AppendRotationDegrees(180)));

        ImageComparer.Exact.VerifySimilarity(img, img2);
        ImageComparer.Exact.VerifySimilarity(img, img3);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void TransformUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 50, 50]);

        ProjectiveTransformBuilder builder = new ProjectiveTransformBuilder()
            .AppendRotationDegrees(180);

        image.Mutate(ctx => ctx.Transform(builder));

        // A 180-degree rotation inverts both axes around the image center.
        // The subject location (5, 15) becomes (imageWidth - 5, imageHeight - 15) = (95, 85)
        Assert.Equal(
            [95, 85],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        // The subject area is also mirrored around the center.
        // New X = imageWidth - originalX - width
        // New Y = imageHeight - originalY - height
        // (5, 15, 50, 50) becomes (45, 35, 50, 50)
        Assert.Equal(
            [45, 35, 50, 50],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }

    private static IResampler GetResampler(string name)
    {
        PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

        if (property is null)
        {
            throw new InvalidOperationException($"No resampler named {name}");
        }

        return (IResampler)property.GetValue(null);
    }
}
