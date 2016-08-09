// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System;
    using Xunit;

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
            ImageProperty property1 = new ImageProperty("Foo", "Bar");
            ImageProperty property2 = new ImageProperty("Foo", "Bar");
            ImageProperty property3 = null;

            Assert.Equal(property1, property2);
            Assert.True(property1 == property2);
            Assert.Equal(property3, null);
        }

        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            ImageProperty property1 = new ImageProperty("Foo", "Bar");
            ImageProperty property2 = new ImageProperty("Foo", "Foo");
            ImageProperty property3 = new ImageProperty("Bar", "Bar");
            ImageProperty property4 = new ImageProperty("Foo", null);

            Assert.False(property1.Equals("Foo"));

            Assert.NotEqual(property1, null);

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
            ImageProperty property = new ImageProperty("Foo", null);
            Assert.Equal("Foo", property.Name);
            Assert.Equal(null, property.Value);

            property = new ImageProperty("Foo", string.Empty);
            Assert.Equal(string.Empty, property.Value);
        }
    }
}
