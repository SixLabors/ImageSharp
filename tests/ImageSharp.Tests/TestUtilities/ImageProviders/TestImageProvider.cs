// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using Castle.Core.Internal;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Provides <see cref="Image{TPixel}" /> instances for parametric unit tests.
/// </summary>
/// <typeparam name="TPixel">The pixel format of the image.</typeparam>
public abstract partial class TestImageProvider<TPixel> : ITestImageProvider, IXunitSerializable
    where TPixel : unmanaged, IPixel<TPixel>
{
    public PixelTypes PixelType { get; private set; } = typeof(TPixel).GetPixelType();

    public virtual string SourceFileOrDescription => string.Empty;

    public Configuration Configuration { get; set; } = Configuration.CreateDefaultInstance();

    /// <summary>
    /// Gets the utility instance to provide information about the test image & manage input/output.
    /// </summary>
    public ImagingTestCaseUtility Utility { get; private set; }

    public string TypeName { get; private set; }

    public string MethodName { get; private set; }

    public string OutputSubfolderName { get; private set; }

    public static TestImageProvider<TPixel> BasicTestPattern(
        int width,
        int height,
        MethodInfo testMethod = null,
        PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new BasicTestPatternProvider(width, height).Init(testMethod, pixelTypeOverride);

    public static TestImageProvider<TPixel> TestPattern(
            int width,
            int height,
            MethodInfo testMethod = null,
            PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new TestPatternProvider(width, height).Init(testMethod, pixelTypeOverride);

    public static TestImageProvider<TPixel> Blank(
        int width,
        int height,
        MethodInfo testMethod = null,
        PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new BlankProvider(width, height).Init(testMethod, pixelTypeOverride);

    public static TestImageProvider<TPixel> File(
        string filePath,
        MethodInfo testMethod = null,
        PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new FileProvider(filePath).Init(testMethod, pixelTypeOverride);

    public static TestImageProvider<TPixel> Lambda(
            string declaringTypeName,
            string methodName,
            MethodInfo testMethod = null,
            PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new MemberMethodProvider(declaringTypeName, methodName).Init(testMethod, pixelTypeOverride);

    public static TestImageProvider<TPixel> Solid(
        int width,
        int height,
        byte r,
        byte g,
        byte b,
        byte a = 255,
        MethodInfo testMethod = null,
        PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        => new SolidProvider(width, height, r, g, b, a).Init(testMethod, pixelTypeOverride);

    /// <summary>
    /// Returns an <see cref="Image{TPixel}"/> instance to the test case with the necessary traits.
    /// </summary>
    /// <returns>A test image.</returns>
    public abstract Image<TPixel> GetImage();

    public Image<TPixel> GetImage(IImageDecoder decoder)
        => this.GetImage(decoder, new DecoderOptions());

    public Task<Image<TPixel>> GetImageAsync(IImageDecoder decoder)
         => this.GetImageAsync(decoder, new DecoderOptions());

    public virtual Image<TPixel> GetImage(IImageDecoder decoder, DecoderOptions options)
        => throw new NotSupportedException($"Decoder specific GetImage() is not supported with {this.GetType().Name}!");

    public virtual Task<Image<TPixel>> GetImageAsync(IImageDecoder decoder, DecoderOptions options)
        => throw new NotSupportedException($"Decoder specific GetImageAsync() is not supported with {this.GetType().Name}!");

    public virtual Image<TPixel> GetImage<T>(ISpecializedImageDecoder<T> decoder, T options)
        where T : class, ISpecializedDecoderOptions, new()
        => throw new NotSupportedException($"Decoder specific GetImage() is not supported with {this.GetType().Name}!");

    public virtual Task<Image<TPixel>> GetImageAsync<T>(ISpecializedImageDecoder<T> decoder, T options)
        where T : class, ISpecializedDecoderOptions, new()
        => throw new NotSupportedException($"Decoder specific GetImageAsync() is not supported with {this.GetType().Name}!");

    /// <summary>
    /// Returns an <see cref="Image{TPixel}"/> instance to the test case with the necessary traits.
    /// </summary>
    /// <param name="operationsToApply">The operation to apply to the image before returning.</param>
    /// <returns>A test image.</returns>
    public Image<TPixel> GetImage(Action<IImageProcessingContext> operationsToApply)
    {
        Image<TPixel> img = this.GetImage();
        img.Mutate(operationsToApply);
        return img;
    }

    public virtual void Deserialize(IXunitSerializationInfo info)
    {
        PixelTypes pixelType = info.GetValue<PixelTypes>("PixelType");
        string typeName = info.GetValue<string>("TypeName");
        string methodName = info.GetValue<string>("MethodName");
        string outputSubfolderName = info.GetValue<string>("OutputSubfolderName");

        this.Init(typeName, methodName, outputSubfolderName, pixelType);
    }

    public virtual void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue("PixelType", this.PixelType);
        info.AddValue("TypeName", this.TypeName);
        info.AddValue("MethodName", this.MethodName);
        info.AddValue("OutputSubfolderName", this.OutputSubfolderName);
    }

    protected TestImageProvider<TPixel> Init(
        string typeName,
        string methodName,
        string outputSubfolderName,
        PixelTypes pixelTypeOverride)
    {
        if (pixelTypeOverride != PixelTypes.Undefined)
        {
            this.PixelType = pixelTypeOverride;
        }

        this.TypeName = typeName;
        this.MethodName = methodName;
        this.OutputSubfolderName = outputSubfolderName;

        this.Utility = new ImagingTestCaseUtility
        {
            SourceFileOrDescription = this.SourceFileOrDescription,
            PixelTypeName = this.PixelType.ToString()
        };

        if (methodName != null)
        {
            this.Utility.Init(typeName, methodName, outputSubfolderName);
        }

        return this;
    }

    protected TestImageProvider<TPixel> Init(MethodInfo testMethod, PixelTypes pixelTypeOverride)
    {
        string subfolder =
            testMethod?.DeclaringType.GetAttribute<GroupOutputAttribute>()?.Subfolder ?? string.Empty;

        return this.Init(testMethod?.DeclaringType.Name, testMethod?.Name, subfolder, pixelTypeOverride);
    }

    public override string ToString()
        => $"{this.SourceFileOrDescription}[{this.PixelType}]";
}
