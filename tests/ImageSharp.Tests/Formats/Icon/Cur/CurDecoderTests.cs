// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon.Cur;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Cur;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Cur;

[Trait("Format", "Cur")]
[ValidateDisposedMemoryAllocations]
public class CurDecoderTests
{
    [Theory]
    [WithFile(WindowsMouse, PixelTypes.Rgba32)]
    public void CurDecoder_Decode(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage(CurDecoder.Instance);

        image.DebugSave(provider, extension: "tiff", encoder: new TiffEncoder());
    }
}
