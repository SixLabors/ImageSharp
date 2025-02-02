// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Ani;

namespace SixLabors.ImageSharp.Tests.Formats.Ani;

[Trait("format", "Ani")]
[ValidateDisposedMemoryAllocations]
public class AniDecoderTests
{
    [Theory]
    [WithFile(Work, PixelTypes.Rgba32)]
    public void CurDecoder_Decode(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(AniDecoder.Instance);
    }
}
