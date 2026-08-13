// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Extensions.Convolution;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Processing.Processors.Normalization;

namespace SixLabors.ImageSharp.Tests.Processing;

[Trait("Category", "Processors")]
public class AlphaAssociationProcessorTests
{
    [Theory]
    [InlineData(ProcessorCase.Opaque)]
    [InlineData(ProcessorCase.Invert)]
    [InlineData(ProcessorCase.ColorMatrix)]
    [InlineData(ProcessorCase.Convolution)]
    [InlineData(ProcessorCase.ConvolutionPreserveAlpha)]
    [InlineData(ProcessorCase.Convolution2D)]
    [InlineData(ProcessorCase.Convolution2DPreserveAlpha)]
    [InlineData(ProcessorCase.Convolution2Pass)]
    [InlineData(ProcessorCase.Convolution2PassPreserveAlpha)]
    [InlineData(ProcessorCase.Median)]
    [InlineData(ProcessorCase.MedianPreserveAlpha)]
    [InlineData(ProcessorCase.BokehBlur)]
    [InlineData(ProcessorCase.OilPaint)]
    [InlineData(ProcessorCase.HistogramGlobal)]
    [InlineData(ProcessorCase.HistogramAdaptiveSlidingWindow)]
    [InlineData(ProcessorCase.HistogramAdaptiveTileInterpolation)]
    [InlineData(ProcessorCase.HistogramAutoLevel)]
    [InlineData(ProcessorCase.HistogramAutoLevelSeparateChannels)]
    [InlineData(ProcessorCase.ResizeCompanded)]
    [InlineData(ProcessorCase.ResizeUnassociated)]
    [InlineData(ProcessorCase.AffineTransform)]
    [InlineData(ProcessorCase.ProjectiveTransform)]
    [InlineData(ProcessorCase.OrderedDither)]
    [InlineData(ProcessorCase.EntropyCrop)]
    public void EquivalentAlphaRepresentationsProduceEquivalentResults(ProcessorCase processor)
    {
        using Image<RgbaVector> unassociated = processor == ProcessorCase.EntropyCrop ? CreateEntropyCropImage() : CreateTestImage();
        using Image<ScaledRgbaVectorP> associated = CreateAssociatedImage(unassociated);

        ApplyProcessor(unassociated, processor);
        ApplyProcessor(associated, processor);

        AssertEquivalent(unassociated, associated);
    }

    /// <summary>
    /// Verifies the native-range bulk hooks required by the public associated-alpha operation contract.
    /// </summary>
    [Fact]
    public void ScaledAssociatedPixelNativeBulkConversionsMatchScalarConversions()
    {
        const int length = 259;
        ScaledRgbaVectorP pixel = ScaledRgbaVectorP.FromUnassociatedScaledVector4(new Vector4(.8F, .4F, .2F, .5F));
        ScaledRgbaVectorP[] pixels = new ScaledRgbaVectorP[length];
        Vector4[] expectedVectors = new Vector4[length];
        Vector4[] actualVectors = new Vector4[length];
        ScaledRgbaVectorP[] expectedPixels = new ScaledRgbaVectorP[length];
        ScaledRgbaVectorP[] actualPixels = new ScaledRgbaVectorP[length];
        PixelOperations<ScaledRgbaVectorP> operations = PixelOperations<ScaledRgbaVectorP>.Instance;

        Array.Fill(pixels, pixel);

        for (int i = 0; i < length; i++)
        {
            expectedVectors[i] = pixels[i].ToUnassociatedVector4();
        }

        operations.ToVector4(Configuration.Default, pixels, actualVectors, PixelConversionModifiers.UnPremultiply);
        Assert.Equal(expectedVectors, actualVectors);

        for (int i = 0; i < length; i++)
        {
            expectedVectors[i] = pixels[i].ToAssociatedVector4();
        }

        operations.ToVector4(Configuration.Default, pixels, actualVectors, PixelConversionModifiers.Premultiply);
        Assert.Equal(expectedVectors, actualVectors);

        for (int i = 0; i < length; i++)
        {
            expectedVectors[i] = pixels[i].ToUnassociatedVector4();
            expectedPixels[i] = ScaledRgbaVectorP.FromUnassociatedVector4(expectedVectors[i]);
        }

        operations.FromVector4Destructive(Configuration.Default, expectedVectors, actualPixels, PixelConversionModifiers.UnPremultiply);
        Assert.Equal(expectedPixels, actualPixels);

        for (int i = 0; i < length; i++)
        {
            expectedVectors[i] = pixels[i].ToAssociatedVector4();
            expectedPixels[i] = ScaledRgbaVectorP.FromAssociatedVector4(expectedVectors[i]);
        }

        operations.FromVector4Destructive(Configuration.Default, expectedVectors, actualPixels, PixelConversionModifiers.Premultiply);
        Assert.Equal(expectedPixels, actualPixels);
    }

    [Fact]
    public void ErrorDiffusionUsesLogicalUnassociatedError()
    {
        Vector4 background = new(.25F, .25F, .25F, .5F);
        using Image<RgbaVector> unassociated = new(4, 4, RgbaVector.FromScaledVector4(background));
        using Image<ScaledRgbaVectorP> associated = CreateAssociatedImage(unassociated);

        Vector4 source = new(.5F, .5F, .5F, .5F);
        Vector4 transformed = new(.25F, .25F, .25F, .5F);
        RgbaVector unassociatedSource = RgbaVector.FromScaledVector4(source);
        RgbaVector unassociatedTransformed = RgbaVector.FromScaledVector4(transformed);
        ScaledRgbaVectorP associatedSource = ScaledRgbaVectorP.FromUnassociatedScaledVector4(source);
        ScaledRgbaVectorP associatedTransformed = ScaledRgbaVectorP.FromUnassociatedScaledVector4(transformed);

        ErrorDither.FloydSteinberg.Dither(unassociated.Frames.RootFrame, unassociated.Bounds, unassociatedSource, unassociatedTransformed, 1, 1, 1F);
        ErrorDither.FloydSteinberg.Dither(associated.Frames.RootFrame, associated.Bounds, associatedSource, associatedTransformed, 1, 1, 1F);

        AssertEquivalent(unassociated, associated);
    }

    [Fact]
    public void AffineTransformFractionalEdgesMatchNormalPixelConversion()
    {
        using Image<Rgba32> rgba = new(4, 4, new Rgba32(255, 64, 16, 255));
        using Image<Bgra32> bgra = rgba.CloneAs<Bgra32>();
        using Image<Rgb24> rgb = rgba.CloneAs<Rgb24>();

        ApplyProcessor(rgba, ProcessorCase.AffineTransform);
        ApplyProcessor(bgra, ProcessorCase.AffineTransform);
        ApplyProcessor(rgb, ProcessorCase.AffineTransform);

        // Alpha-less output drops fractional coverage after unassociation, just like a normal pixel-format conversion.
        using Image<Bgra32> expectedBgra = rgba.CloneAs<Bgra32>();
        using Image<Rgb24> expectedRgb = rgba.CloneAs<Rgb24>();

        Assert.Equal(rgba.Size, bgra.Size);
        Assert.Equal(rgba.Size, rgb.Size);

        bool foundFractionalCoverage = false;
        for (int y = 0; y < rgba.Height; y++)
        {
            for (int x = 0; x < rgba.Width; x++)
            {
                Rgba32 pixel = rgba[x, y];

                if (pixel.A is > 0 and < 255)
                {
                    foundFractionalCoverage = true;
                    Assert.Equal(new Rgba32(255, 64, 16, pixel.A), pixel);
                }

                Assert.Equal(expectedBgra[x, y], bgra[x, y]);
                Assert.Equal(expectedRgb[x, y], rgb[x, y]);
            }
        }

        Assert.True(foundFractionalCoverage);
    }

    [Fact]
    public void ConvolutionDoesNotObserveColorBehindZeroAlpha()
    {
        using Image<RgbaVector> hiddenColor = new(3, 1);
        using Image<RgbaVector> transparentBlack = new(3, 1);

        hiddenColor[0, 0] = new RgbaVector(1, 0, 0, 0);
        hiddenColor[1, 0] = new RgbaVector(0, 0, 1, 1);
        hiddenColor[2, 0] = new RgbaVector(1, 0, 0, 0);
        transparentBlack[1, 0] = hiddenColor[1, 0];

        ApplyProcessor(hiddenColor, ProcessorCase.Convolution);
        ApplyProcessor(transparentBlack, ProcessorCase.Convolution);

        // Associated filtering makes fully transparent RGB unobservable before the kernel is evaluated.
        for (int x = 0; x < hiddenColor.Width; x++)
        {
            Assert.Equal(transparentBlack[x, 0], hiddenColor[x, 0]);
        }
    }

    private static Image<RgbaVector> CreateTestImage()
    {
        Image<RgbaVector> image = new(8, 8);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                float alpha = ((x + (2 * y)) % 3) switch
                {
                    0 => .25F,
                    1 => .5F,
                    _ => 1F
                };

                // Dyadic components and alpha make the initial association round trip exact in binary floating point.
                float red = ((x + y) & 7) / 8F;
                float green = (((3 * x) + y + 1) & 7) / 8F;
                float blue = ((x + (5 * y) + 2) & 7) / 8F;
                image[x, y] = new RgbaVector(red, green, blue, alpha);
            }
        }

        return image;
    }

    private static Image<RgbaVector> CreateEntropyCropImage()
    {
        Image<RgbaVector> image = new(8, 8, new RgbaVector(0, 0, 0, 1));

        for (int y = 2; y < 6; y++)
        {
            for (int x = 2; x < 6; x++)
            {
                image[x, y] = new RgbaVector(1, 1, 1, 1);
            }
        }

        return image;
    }

    private static Image<ScaledRgbaVectorP> CreateAssociatedImage(Image<RgbaVector> source)
    {
        Image<ScaledRgbaVectorP> image = new(source.Width, source.Height);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Vector4 vector = source[x, y].ToScaledVector4();
                Numerics.Premultiply(ref vector);
                image[x, y] = ScaledRgbaVectorP.FromScaledVector4(vector);
            }
        }

        return image;
    }

    private static void ApplyProcessor<TPixel>(Image<TPixel> image, ProcessorCase processor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (processor == ProcessorCase.Opaque)
        {
            using OpaqueProcessor<TPixel> opaque = new(image.Configuration, image, image.Bounds);
            opaque.Apply(image.Frames.RootFrame);
            return;
        }

        if (processor is ProcessorCase.Convolution2D or ProcessorCase.Convolution2DPreserveAlpha)
        {
            DenseMatrix<float> kernelX = new float[,]
            {
                { -.25F, 0, .25F },
                { -.5F, 0, .5F },
                { -.25F, 0, .25F }
            };
            DenseMatrix<float> kernelY = new float[,]
            {
                { -.25F, -.5F, -.25F },
                { 0, 0, 0 },
                { .25F, .5F, .25F }
            };

            using Convolution2DProcessor<TPixel> convolution = new(
                image.Configuration,
                kernelX,
                kernelY,
                processor == ProcessorCase.Convolution2DPreserveAlpha,
                image,
                image.Bounds);

            convolution.Apply(image.Frames.RootFrame);
            return;
        }

        if (processor is ProcessorCase.Convolution2Pass or ProcessorCase.Convolution2PassPreserveAlpha)
        {
            float[] kernel = [.25F, .5F, .25F];
            using Convolution2PassProcessor<TPixel> convolution = new(
                image.Configuration,
                kernel,
                processor == ProcessorCase.Convolution2PassPreserveAlpha,
                image,
                image.Bounds,
                BorderWrappingMode.Repeat,
                BorderWrappingMode.Repeat);

            convolution.Apply(image.Frames.RootFrame);
            return;
        }

        image.Mutate(context =>
        {
            switch (processor)
            {
                case ProcessorCase.Invert:
                    context.Invert();
                    break;
                case ProcessorCase.ColorMatrix:
                    ColorMatrix matrix = new()
                    {
                        M11 = .5F,
                        M22 = .5F,
                        M33 = .5F,
                        M41 = .25F,
                        M42 = .25F,
                        M43 = .25F,
                        M14 = .125F,
                        M24 = .125F,
                        M34 = .125F,
                        M44 = .5F
                    };

                    context.Filter(matrix);
                    break;
                case ProcessorCase.Convolution:
                    context.Convolve(new float[,] { { .25F, .5F, .25F } });
                    break;
                case ProcessorCase.ConvolutionPreserveAlpha:
                    context.Convolve(new float[,] { { .25F, .5F, .25F } }, true);
                    break;
                case ProcessorCase.Median:
                    context.MedianBlur(1, false);
                    break;
                case ProcessorCase.MedianPreserveAlpha:
                    context.MedianBlur(1, true);
                    break;
                case ProcessorCase.BokehBlur:
                    context.BokehBlur(2, 2, 2F);
                    break;
                case ProcessorCase.OilPaint:
                    context.OilPaint(4, 3);
                    break;
                case ProcessorCase.HistogramGlobal:
                    context.HistogramEqualization(CreateHistogramOptions(HistogramEqualizationMethod.Global));
                    break;
                case ProcessorCase.HistogramAdaptiveSlidingWindow:
                    context.HistogramEqualization(CreateHistogramOptions(HistogramEqualizationMethod.AdaptiveSlidingWindow));
                    break;
                case ProcessorCase.HistogramAdaptiveTileInterpolation:
                    context.HistogramEqualization(CreateHistogramOptions(HistogramEqualizationMethod.AdaptiveTileInterpolation));
                    break;
                case ProcessorCase.HistogramAutoLevel:
                    context.HistogramEqualization(CreateHistogramOptions(HistogramEqualizationMethod.AutoLevel));
                    break;
                case ProcessorCase.HistogramAutoLevelSeparateChannels:
                    HistogramEqualizationOptions options = CreateHistogramOptions(HistogramEqualizationMethod.AutoLevel);
                    options.SyncChannels = false;
                    context.HistogramEqualization(options);
                    break;
                case ProcessorCase.ResizeCompanded:
                    context.Resize(new ResizeOptions
                    {
                        Size = new Size(5, 5),
                        Sampler = KnownResamplers.Box,
                        Compand = true,
                        PremultiplyAlpha = true
                    });
                    break;
                case ProcessorCase.ResizeUnassociated:
                    context.Resize(new ResizeOptions
                    {
                        Size = new Size(5, 5),
                        Sampler = KnownResamplers.Box,
                        PremultiplyAlpha = false
                    });
                    break;
                case ProcessorCase.AffineTransform:
                    context.Transform(image.Bounds, Matrix3x2.CreateTranslation(.5F, .5F), image.Size, KnownResamplers.Bicubic);
                    break;
                case ProcessorCase.ProjectiveTransform:
                    context.Transform(image.Bounds, Matrix4x4.CreateTranslation(.5F, .5F, 0), image.Size, KnownResamplers.Bicubic);
                    break;
                case ProcessorCase.OrderedDither:
                    context.Dither(KnownDitherings.Bayer4x4);
                    break;
                case ProcessorCase.EntropyCrop:
                    context.EntropyCrop(.5F);
                    break;
            }
        });
    }

    private static HistogramEqualizationOptions CreateHistogramOptions(HistogramEqualizationMethod method)
        => new()
        {
            Method = method,
            LuminanceLevels = 16,
            NumberOfTiles = 2
        };

    private static void AssertEquivalent(Image<RgbaVector> unassociated, Image<ScaledRgbaVectorP> associated)
    {
        // Materialize both representations through the same 16-bit logical-color boundary. Their floating-point storage paths can differ by one rounding operation when associated results are unassociated for RgbaVector storage.
        using Image<Rgba64> expected = unassociated.CloneAs<Rgba64>();
        using Image<Rgba64> actual = associated.CloneAs<Rgba64>();

        Assert.Equal(expected.Size, actual.Size);

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    public enum ProcessorCase
    {
        Opaque,
        Invert,
        ColorMatrix,
        Convolution,
        ConvolutionPreserveAlpha,
        Convolution2D,
        Convolution2DPreserveAlpha,
        Convolution2Pass,
        Convolution2PassPreserveAlpha,
        Median,
        MedianPreserveAlpha,
        BokehBlur,
        OilPaint,
        HistogramGlobal,
        HistogramAdaptiveSlidingWindow,
        HistogramAdaptiveTileInterpolation,
        HistogramAutoLevel,
        HistogramAutoLevelSeparateChannels,
        ResizeCompanded,
        ResizeUnassociated,
        AffineTransform,
        ProjectiveTransform,
        OrderedDither,
        EntropyCrop
    }
}
