// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
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
            var property1 = new PngTextData("Foo", "Bar", "foo", "bar");
            var property2 = new PngTextData("Foo", "Bar", "foo", "bar");

            Assert.Equal(property1, property2);
            Assert.True(property1 == property2);
        }

        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var property1 = new PngTextData("Foo", "Bar", "foo", "bar");
            var property2 = new PngTextData("Foo", "Foo", string.Empty, string.Empty);
            var property3 = new PngTextData("Bar", "Bar", "unit", "test");
            var property4 = new PngTextData("Foo", null, "test", "case");

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
            var property = new PngTextData("Foo", null, "unit", "test");
            Assert.Equal("Foo", property.Keyword);
            Assert.Null(property.Value);
            Assert.Equal("unit", property.LanguageTag);
            Assert.Equal("test", property.TranslatedKeyword);

            property = new PngTextData("Foo", string.Empty, string.Empty, null);
            Assert.Equal("Foo", property.Keyword);
            Assert.Equal(string.Empty, property.Value);
            Assert.Equal(string.Empty, property.LanguageTag);
            Assert.Null(property.TranslatedKeyword);
        }
    }
}
