// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Cur;

namespace SixLabors.ImageSharp.Tests.Formats.Icon.Cur;

[Trait("Format", "Cur")]
public class CurEncoderTests
{
    private static CurEncoder CurEncoder => new();

    public static readonly TheoryData<string> Files = new()
    {
        { WindowsMouse },
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
        CurDecoder.Instance.Decode(new(), memStream);
    }

    [Theory]
    [WithFile(CurFake, PixelTypes.Rgba32)]
    [WithFile(CurReal, PixelTypes.Rgba32)]
    public void CurDecoder_Decode2(TestImageProvider<Rgba32> provider)
    {
        Debug.Assert(false);
    }
}
