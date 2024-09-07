// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public abstract partial class ImageFrameCollectionTests : IDisposable
{
    protected Image<Rgba32> Image { get; }

    protected ImageFrameCollection<Rgba32> Collection { get; }

    public ImageFrameCollectionTests()
    {
        // Needed to get English exception messages, which are checked in several tests.
        System.Threading.Thread.CurrentThread.CurrentUICulture = new("en-US");

        this.Image = new(10, 10);
        this.Collection = new(this.Image, 10, 10, default(Rgba32));
    }

    public void Dispose()
    {
        this.Image.Dispose();
        this.Collection.Dispose();
    }
}
