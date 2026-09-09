// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Decodes an auxiliary item into native samples for joint color and alpha conversion.
/// </summary>
internal interface IHeifAlphaItemDecoder
{
    /// <summary>
    /// Decodes the auxiliary item and transfers its native sample plane to the caller.
    /// </summary>
    /// <param name="options">The options governing payload validation and allocation.</param>
    /// <param name="item">The auxiliary image item.</param>
    /// <param name="data">The encoded auxiliary payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The native plane owned by the caller.</returns>
    public Av1FrameBuffer<byte> DecodeAlphaItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CancellationToken cancellationToken);
}
