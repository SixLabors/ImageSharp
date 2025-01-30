// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing;

public abstract class BaseImageOperationsExtensionTest : IDisposable
{
    protected readonly IImageProcessingContext operations;
    private readonly FakeImageOperationsProvider.FakeImageOperations<Rgba32> internalOperations;
    protected readonly Rectangle rect;
    protected readonly GraphicsOptions options;
    private readonly Image<Rgba32> source;

    public Rectangle SourceBounds() => this.source.Bounds;

    public BaseImageOperationsExtensionTest()
    {
        this.options = new() { Antialias = false };
        this.source = new(91 + 324, 123 + 56);
        this.rect = new(91, 123, 324, 56); // make this random?
        this.internalOperations = new(this.source.Configuration, this.source, false);
        this.internalOperations.SetGraphicsOptions(this.options);
        this.operations = this.internalOperations;
    }

    public T Verify<T>(int index = 0)
    {
        Assert.InRange(index, 0, this.internalOperations.Applied.Count - 1);

        FakeImageOperationsProvider.FakeImageOperations<Rgba32>.AppliedOperation operation = this.internalOperations.Applied[index];

        if (operation.NonGenericProcessor != null)
        {
            return Assert.IsType<T>(operation.NonGenericProcessor);
        }

        return Assert.IsType<T>(operation.GenericProcessor);
    }

    public T Verify<T>(Rectangle rect, int index = 0)
    {
        Assert.InRange(index, 0, this.internalOperations.Applied.Count - 1);

        FakeImageOperationsProvider.FakeImageOperations<Rgba32>.AppliedOperation operation = this.internalOperations.Applied[index];

        Assert.Equal(rect, operation.Rectangle);

        if (operation.NonGenericProcessor != null)
        {
            return Assert.IsType<T>(operation.NonGenericProcessor);
        }

        return Assert.IsType<T>(operation.GenericProcessor);
    }

    public void Dispose() => this.source?.Dispose();
}
