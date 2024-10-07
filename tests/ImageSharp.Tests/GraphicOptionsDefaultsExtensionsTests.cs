// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.Processing;

namespace SixLabors.ImageSharp.Tests;

public class GraphicOptionsDefaultsExtensionsTests
{
    [Fact]
    public void SetDefaultOptionsOnProcessingContext()
    {
        GraphicsOptions option = new();
        Configuration config = new();
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);

        context.SetGraphicsOptions(option);

        // sets the prop on the processing context not on the configuration
        Assert.Equal(option, context.Properties[typeof(GraphicsOptions)]);
        Assert.DoesNotContain(typeof(GraphicsOptions), config.Properties.Keys);
    }

    [Fact]
    public void UpdateDefaultOptionsOnProcessingContext_AlwaysNewInstance()
    {
        GraphicsOptions option = new()
        {
            BlendPercentage = 0.9f
        };
        Configuration config = new();
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);
        context.SetGraphicsOptions(option);

        context.SetGraphicsOptions(o =>
        {
            Assert.Equal(0.9f, o.BlendPercentage); // has origional values
            o.BlendPercentage = 0.4f;
        });

        GraphicsOptions returnedOption = context.GetGraphicsOptions();
        Assert.Equal(0.4f, returnedOption.BlendPercentage);
        Assert.Equal(0.9f, option.BlendPercentage); // hasn't been mutated
    }

    [Fact]
    public void SetDefaultOptionsOnConfiguration()
    {
        GraphicsOptions option = new();
        Configuration config = new();

        config.SetGraphicsOptions(option);

        Assert.Equal(option, config.Properties[typeof(GraphicsOptions)]);
    }

    [Fact]
    public void UpdateDefaultOptionsOnConfiguration_AlwaysNewInstance()
    {
        GraphicsOptions option = new()
        {
            BlendPercentage = 0.9f
        };
        Configuration config = new();
        config.SetGraphicsOptions(option);

        config.SetGraphicsOptions(o =>
        {
            Assert.Equal(0.9f, o.BlendPercentage); // has origional values
            o.BlendPercentage = 0.4f;
        });

        GraphicsOptions returnedOption = config.GetGraphicsOptions();
        Assert.Equal(0.4f, returnedOption.BlendPercentage);
        Assert.Equal(0.9f, option.BlendPercentage); // hasn't been mutated
    }

    [Fact]
    public void GetDefaultOptionsFromConfiguration_SettingNullThenReturnsNewInstance()
    {
        Configuration config = new();

        GraphicsOptions options = config.GetGraphicsOptions();
        Assert.NotNull(options);
        config.SetGraphicsOptions((GraphicsOptions)null);

        GraphicsOptions options2 = config.GetGraphicsOptions();
        Assert.NotNull(options2);

        // we set it to null should now be a new instance
        Assert.NotEqual(options, options2);
    }

    [Fact]
    public void GetDefaultOptionsFromConfiguration_IgnoreIncorectlyTypesDictionEntry()
    {
        Configuration config = new();

        config.Properties[typeof(GraphicsOptions)] = "wronge type";
        GraphicsOptions options = config.GetGraphicsOptions();
        Assert.NotNull(options);
        Assert.IsType<GraphicsOptions>(options);
    }

    [Fact]
    public void GetDefaultOptionsFromConfiguration_AlwaysReturnsInstance()
    {
        Configuration config = new();

        Assert.DoesNotContain(typeof(GraphicsOptions), config.Properties.Keys);
        GraphicsOptions options = config.GetGraphicsOptions();
        Assert.NotNull(options);
    }

    [Fact]
    public void GetDefaultOptionsFromConfiguration_AlwaysReturnsSameValue()
    {
        Configuration config = new();

        GraphicsOptions options = config.GetGraphicsOptions();
        GraphicsOptions options2 = config.GetGraphicsOptions();
        Assert.Equal(options, options2);
    }

    [Fact]
    public void GetDefaultOptionsFromProcessingContext_AlwaysReturnsInstance()
    {
        Configuration config = new();
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);

        GraphicsOptions ctxOptions = context.GetGraphicsOptions();
        Assert.NotNull(ctxOptions);
    }

    [Fact]
    public void GetDefaultOptionsFromProcessingContext_AlwaysReturnsInstanceEvenIfSetToNull()
    {
        Configuration config = new();
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);

        context.SetGraphicsOptions((GraphicsOptions)null);
        GraphicsOptions ctxOptions = context.GetGraphicsOptions();
        Assert.NotNull(ctxOptions);
    }

    [Fact]
    public void GetDefaultOptionsFromProcessingContext_FallbackToConfigsInstance()
    {
        GraphicsOptions option = new();
        Configuration config = new();
        config.SetGraphicsOptions(option);
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);

        GraphicsOptions ctxOptions = context.GetGraphicsOptions();
        Assert.Equal(option, ctxOptions);
    }

    [Fact]
    public void GetDefaultOptionsFromProcessingContext_IgnoreIncorectlyTypesDictionEntry()
    {
        Configuration config = new();
        FakeImageOperationsProvider.FakeImageOperations<Rgba32> context = new(config, null, true);
        context.Properties[typeof(GraphicsOptions)] = "wronge type";
        GraphicsOptions options = context.GetGraphicsOptions();
        Assert.NotNull(options);
        Assert.IsType<GraphicsOptions>(options);
    }

    [Theory]
    [WithBlankImages(100, 100, PixelTypes.Rgba32)]
    public void CanGetGraphicsOptionsMultiThreaded<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Could not get fake operations to trigger #1230 so using a real image.
        Parallel.For(0, 10, _ =>
        {
            using Image<TPixel> image = provider.GetImage();
            image.Mutate(x => x.BackgroundColor(Color.White));
        });
    }
}
