// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon.Ico;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
[ValidateDisposedMemoryAllocations]
public class IcoDecoderTests
{
    [Theory]
    [WithFile(Flutter, PixelTypes.Rgba32)]
    public void IcoDecoder_Decode(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(IcoDecoder.Instance);

        image.DebugSave(provider, extension: "tiff", encoder: new TiffEncoder());
    }
}
