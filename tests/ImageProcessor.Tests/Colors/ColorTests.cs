// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorTests.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Tests the <see cref="Color" /> struct.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Color"/> struct.
    /// </summary>
    public class ColorTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Color color1 = new Color(0, 0, 0);
            Color color2 = new Color(0, 0, 0, 1);
            Color color3 = new Color("#000");
            Color color4 = new Color("#000000");
            Color color5 = new Color("#FF000000");

            Assert.Equal(color1, color2);
            Assert.Equal(color1, color3);
            Assert.Equal(color1, color4);
            Assert.Equal(color1, color5);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Color color1 = new Color(255, 0, 0, 255);
            Color color2 = new Color(0, 0, 0, 255);
            Color color3 = new Color("#000");
            Color color4 = new Color("#000000");
            Color color5 = new Color("#FF000000");

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color1, color3);
            Assert.NotEqual(color1, color4);
            Assert.NotEqual(color1, color5);
        }

        /// <summary>
        /// Tests whether the color constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Color color1 = new Color(1, .1f, .133f, .864f);
            Assert.Equal(1, color1.R, 1);
            Assert.Equal(.1f, color1.G, 1);
            Assert.Equal(.133f, color1.B, 3);
            Assert.Equal(.864f, color1.A, 3);

            Color color2 = new Color(1, .1f, .133f);
            Assert.Equal(1, color2.R, 1);
            Assert.Equal(.1f, color2.G, 1);
            Assert.Equal(.133f, color2.B, 3);
            Assert.Equal(1, color2.A, 1);

            Color color3 = new Color("#FF0000");
            Assert.Equal(1, color3.R, 1);
            Assert.Equal(0, color3.G, 1);
            Assert.Equal(0, color3.B, 3);
            Assert.Equal(1, color3.A, 1);
        }

        /// <summary>
        /// Tests to see that in the input hex matches that of the output.
        /// </summary>
        [Fact]
        public void ConvertHex()
        {
            const string First = "FF000000";
            Bgra32 bgra = new Color(0, 0, 0, 1);
            string second = bgra.Bgra.ToString("X");
            Assert.Equal(First, second);
        }
    }
}
