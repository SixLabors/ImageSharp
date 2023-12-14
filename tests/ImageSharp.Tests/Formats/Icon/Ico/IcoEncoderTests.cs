// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Ico;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Ico;

[Trait("Format", "Icon")]
public class IcoEncoderTests
{
    private static IcoEncoder CurEncoder => new();

    public static readonly TheoryData<string> Files = new()
    {
        { Flutter },
    };

    [Theory]
    [MemberData(nameof(Files))]
    public void Encode(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, CurEncoder);

        memStream.Seek(0, SeekOrigin.Begin);
        IcoDecoder.Instance.Decode(new(), memStream);
    }
}
