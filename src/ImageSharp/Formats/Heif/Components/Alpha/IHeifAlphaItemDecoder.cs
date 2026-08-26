// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Decodes one coded HEIF auxiliary alpha item directly into a packed color frame.
/// </summary>
/// <typeparam name="TPixel">The destination color pixel type.</typeparam>
internal interface IHeifAlphaItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Decodes and composes one coded auxiliary alpha item.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The auxiliary image item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded auxiliary payload.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    public void DecodeAlphaItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        ImageFrame<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        CancellationToken cancellationToken);
}
