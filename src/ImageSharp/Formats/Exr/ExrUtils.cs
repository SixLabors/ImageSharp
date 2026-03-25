// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Formats.Exr;

internal static class ExrUtils
{
    public static uint CalculateBytesPerRow(IList<ExrChannelInfo> channels, uint width)
    {
        uint bytesPerRow = 0;
        foreach (ExrChannelInfo channelInfo in channels)
        {
            if (channelInfo.ChannelName.Equals("A", StringComparison.Ordinal)
                || channelInfo.ChannelName.Equals("R", StringComparison.Ordinal)
                || channelInfo.ChannelName.Equals("G", StringComparison.Ordinal)
                || channelInfo.ChannelName.Equals("B", StringComparison.Ordinal)
                || channelInfo.ChannelName.Equals("Y", StringComparison.Ordinal))
            {
                if (channelInfo.PixelType == ExrPixelType.Half)
                {
                    bytesPerRow += 2 * width;
                }
                else
                {
                    bytesPerRow += 4 * width;
                }
            }
        }

        return bytesPerRow;
    }

    public static uint RowsPerBlock(ExrCompression compression) => compression switch
    {
        ExrCompression.Zip or ExrCompression.Pxr24 => 16,
        ExrCompression.B44 or ExrCompression.B44A or ExrCompression.Piz => 32,
        _ => 1,
    };
}
