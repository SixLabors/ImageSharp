// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Formats.Exr;

internal static class ExrUtils
{
    /// <summary>
    /// The DWAA compression value.
    /// </summary>
    private const ExrCompression Dwaa = (ExrCompression)8;

    /// <summary>
    /// The DWAB compression value.
    /// </summary>
    private const ExrCompression Dwab = (ExrCompression)9;

    /// <summary>
    /// The HTJ2K256 compression value.
    /// </summary>
    private const ExrCompression Htj2k256 = (ExrCompression)10;

    /// <summary>
    /// The HTJ2K32 compression value.
    /// </summary>
    private const ExrCompression Htj2k32 = (ExrCompression)11;

    /// <summary>
    /// The LJ2K compression value.
    /// </summary>
    private const ExrCompression Lj2k = (ExrCompression)12;

    /// <summary>
    /// Calculates the number of bytes in a pixel row. A row holds the samples of every channel, in the order of the channel list.
    /// </summary>
    /// <param name="channels">The image channels array.</param>
    /// <param name="width">The width in pixels of a row.</param>
    /// <returns>The number of bytes per row.</returns>
    public static ulong CalculateBytesPerRow(IList<ExrChannelInfo> channels, uint width)
    {
        ulong bytesPerRow = 0;
        for (int i = 0; i < channels.Count; i++)
        {
            bytesPerRow += (channels[i].PixelType == ExrPixelType.Half ? 2UL : 4UL) * width;
        }

        return bytesPerRow;
    }

    /// <summary>
    /// Determines how many pixel rows there are in a block. This varies depending on the compression used.
    /// </summary>
    /// <param name="compression">The compression used.</param>
    /// <returns>Pixel rows in a block.</returns>
    /// <remarks>
    /// The decoder does not support DWAA, DWAB, HTJ2K and LJ2K. But the decoder needs their rows per block to find the size of the offset table.
    /// </remarks>
    public static uint RowsPerBlock(ExrCompression compression) => compression switch
    {
        ExrCompression.Zip or ExrCompression.Pxr24 => 16,
        ExrCompression.B44 or ExrCompression.B44A or ExrCompression.Piz or Dwaa or Htj2k32 => 32,
        Dwab or Htj2k256 or Lj2k => 256,
        _ => 1,
    };
}
