// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="ImageProperty"/> class.
    /// </summary>
    public class ImagePropertyTests
    {
        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var property1 = new ImageProperty("Foo", "Bar");
            var property2 = new ImageProperty("Foo", "Bar");

            Assert.Equal(property1, property2);
            Assert.True(property1 == property2);
        }

        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var property1 = new ImageProperty("Foo", "Bar");
            var property2 = new ImageProperty("Foo", "Foo");
            var property3 = new ImageProperty("Bar", "Bar");
            var property4 = new ImageProperty("Foo", null);

            Assert.False(property1.Equals("Foo"));

            Assert.NotEqual(property1, property2);
            Assert.True(property1 != property2);

            Assert.NotEqual(property1, property3);
            Assert.NotEqual(property1, property4);
        }

        /// <summary>
        /// Tests whether the constructor throws an exception when the property name is null or empty.
        /// </summary>
        [Fact]
        public void ConstructorThrowsWhenNameIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new ImageProperty(null, "Foo"));

            Assert.Throws<ArgumentException>(() => new ImageProperty(string.Empty, "Foo"));
        }

        /// <summary>
        /// Tests whether the constructor correctly assigns properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var property = new ImageProperty("Foo", null);
            Assert.Equal("Foo", property.Name);
            Assert.Null(property.Value);

            property = new ImageProperty("Foo", string.Empty);
            Assert.Equal(string.Empty, property.Value);
        }
    }
}
