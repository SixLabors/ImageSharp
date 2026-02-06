// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Tests.Common;

public class EncoderExtensionsTests
{
    [Fact]
    public void GetString_EmptyBuffer_ReturnsEmptyString()
    {
        ReadOnlySpan<byte> buffer = default(ReadOnlySpan<byte>);

        string result = Encoding.UTF8.GetString(buffer);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetString_Buffer_ReturnsString()
    {
        ReadOnlySpan<byte> buffer = new("ImageSharp"u8.ToArray());

        string result = Encoding.UTF8.GetString(buffer);

        Assert.Equal("ImageSharp", result);
    }
}
