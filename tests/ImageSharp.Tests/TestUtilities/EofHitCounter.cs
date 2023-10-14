// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

internal class EofHitCounter : IDisposable
{
    private readonly BufferedReadStream stream;

    public EofHitCounter(BufferedReadStream stream, Image image)
    {
        this.stream = stream;
        this.Image = image;
    }

    public int EofHitCount => this.stream.EofHitCount;

    public Image Image { get; private set; }

    public static EofHitCounter RunDecoder(string testImage) => RunDecoder(TestFile.Create(testImage).Bytes);

    public static EofHitCounter RunDecoder(byte[] imageData)
    {
        BufferedReadStream stream = new(Configuration.Default, new MemoryStream(imageData));
        Image image = Image.Load(stream);
        return new EofHitCounter(stream, image);
    }

    public void Dispose()
    {
        this.stream.Dispose();
        this.Image.Dispose();
    }
}
