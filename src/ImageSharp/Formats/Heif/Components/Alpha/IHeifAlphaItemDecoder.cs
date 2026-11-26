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
    /// <param name="options">The options for payload validation and allocation.</param>
    /// <param name="item">The auxiliary image item.</param>
    /// <param name="data">The encoded auxiliary payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The native plane that the caller owns. The value is <see langword="null"/> if the AV1 code reported damage that the mode ignores, and the auxiliary
    /// item gives no plane.
    /// </returns>
    public Av1FrameBuffer<byte>? DecodeAlphaItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CancellationToken cancellationToken);
}
