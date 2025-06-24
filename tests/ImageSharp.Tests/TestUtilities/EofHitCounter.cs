// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
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

    public static EofHitCounter RunDecoder<T, TO>(string testImage, T decoder, TO options)
        where T : SpecializedImageDecoder<TO>
        where TO : ISpecializedDecoderOptions
        => RunDecoder(TestFile.Create(testImage).Bytes, decoder, options);

    public static EofHitCounter RunDecoder(byte[] imageData)
    {
        BufferedReadStream stream = new(Configuration.Default, new MemoryStream(imageData));
        Image image = Image.Load(stream);
        return new(stream, image);
    }

    public static EofHitCounter RunDecoder<T, TO>(byte[] imageData, T decoder, TO options)
        where T : SpecializedImageDecoder<TO>
        where TO : ISpecializedDecoderOptions
    {
        BufferedReadStream stream = new(options.GeneralOptions.Configuration, new MemoryStream(imageData));
        Image image = decoder.Decode(options, stream);
        return new(stream, image);
    }

    public void Dispose()
    {
        this.stream.Dispose();
        this.Image.Dispose();
    }
}
