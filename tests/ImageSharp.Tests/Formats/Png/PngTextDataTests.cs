// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Chunks;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

/// <summary>
/// Tests the <see cref="PngTextData"/> class.
/// </summary>
[Trait("Format", "Png")]
public class PngTextDataTests
{
    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreEqual()
    {
        PngTextData property1 = new("Foo", "Bar", "foo", "bar");
        PngTextData property2 = new("Foo", "Bar", "foo", "bar");

        Assert.Equal(property1, property2);
        Assert.True(property1 == property2);
    }

    /// <summary>
    /// Tests the equality operators for equality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        PngTextData property1 = new("Foo", "Bar", "foo", "bar");
        PngTextData property2 = new("Foo", "Foo", string.Empty, string.Empty);
        PngTextData property3 = new("Bar", "Bar", "unit", "test");
        PngTextData property4 = new("Foo", null, "test", "case");

        Assert.NotEqual(property1, property2);
        Assert.True(property1 != property2);

        Assert.NotEqual(property1, property3);
        Assert.NotEqual(property1, property4);
    }

    /// <summary>
    /// Tests whether the constructor throws an exception when the property keyword is null or empty.
    /// </summary>
    [Fact]
    public void ConstructorThrowsWhenKeywordIsNullOrEmpty()
    {
        Assert.Throws<ArgumentNullException>(() => new PngTextData(null, "Foo", "foo", "bar"));

        Assert.Throws<ArgumentException>(() => new PngTextData(string.Empty, "Foo", "foo", "bar"));
    }

    /// <summary>
    /// Tests whether the constructor correctly assigns properties.
    /// </summary>
    [Fact]
    public void ConstructorAssignsProperties()
    {
        PngTextData property = new("Foo", null, "unit", "test");
        Assert.Equal("Foo", property.Keyword);
        Assert.Null(property.Value);
        Assert.Equal("unit", property.LanguageTag);
        Assert.Equal("test", property.TranslatedKeyword);

        property = new("Foo", string.Empty, string.Empty, null);
        Assert.Equal("Foo", property.Keyword);
        Assert.Equal(string.Empty, property.Value);
        Assert.Equal(string.Empty, property.LanguageTag);
        Assert.Null(property.TranslatedKeyword);
    }
}
