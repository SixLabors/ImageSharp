// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp.Formats.Qoi;
internal class QoiDecoder : ImageDecoder
{
    private QoiDecoder()
    {
    }

    public static QoiDecoder Instance { get; } = new();

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        throw new NotImplementedException();
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        throw new NotImplementedException();
    }

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        return new QoiDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }
}
