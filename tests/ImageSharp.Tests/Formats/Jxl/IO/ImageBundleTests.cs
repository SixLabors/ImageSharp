// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.IO;

public class ImageBundleTests
{
    [Fact]
    public void ExtraChannel()
    {
        using MemoryStream ms = new();
        JxlBitWriter bw = new(ms);

        Assert.True(bw.WithMaxBits(99, () =>
        {
            JxlImageMetadata md = new()
            {
                ExtraChannels =
                [
                    new JxlExtraChannelInfo()
                    {
                        Type = JxlExtraChannel.Black,
                        Name = "testK"
                    }
                ],
                ExtraChannelCount = 1
            };

            if (!JxlBundle.Write(md, bw, JxlLayerType.Header, new JxlAuxiliaryOutput()))
            {
                return false;
            }

            bw.ZeroPadToByte();
            return true;
        }));

        ms.Position = 0;
        JxlBitReader reader = new(ms);
        JxlImageMetadata result = new();

        Assert.True(JxlBundle.Read(reader, result));
        Assert.Equal("testK", result.FindExtraChannel(JxlExtraChannel.Black)?.Name);
    }
}
