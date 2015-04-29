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
    /// Tests the <see cref="Bgra"/> struct.
    /// </summary>
    public class ColorTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Bgra color1 = new Bgra(0, 0, 0, 255);
            Bgra color2 = new Bgra(0, 0, 0, 255);
            Bgra color3 = new Bgra("#000");
            Bgra color4 = new Bgra("#000000");
            Bgra color5 = new Bgra("#FF000000");
            Bgra color6 = new Bgra(-16777216);

            Assert.Equal(color1, color2);
            Assert.Equal(color1, color3);
            Assert.Equal(color1, color4);
            Assert.Equal(color1, color5);
            Assert.Equal(color1, color6);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Bgra color1 = new Bgra(255, 0, 0, 255);
            Bgra color2 = new Bgra(0, 0, 0, 255);
            Bgra color3 = new Bgra("#000");
            Bgra color4 = new Bgra("#000000");
            Bgra color5 = new Bgra("#FF000000");
            Bgra color6 = new Bgra(-16777216);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color1, color3);
            Assert.NotEqual(color1, color4);
            Assert.NotEqual(color1, color5);
            Assert.NotEqual(color1, color6);
        }

        /// <summary>
        /// Tests whether the color constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Bgra color1 = new Bgra(255, 10, 34, 220);
            Assert.Equal(255, color1.B);
            Assert.Equal(10, color1.G);
            Assert.Equal(34, color1.R);
            Assert.Equal(220, color1.A);

            Bgra color2 = new Bgra(255, 10, 34);
            Assert.Equal(255, color2.B);
            Assert.Equal(10, color2.G);
            Assert.Equal(34, color2.R);
            Assert.Equal(255, color2.A);

            Bgra color3 = new Bgra(-1);
            Assert.Equal(255, color3.B);
            Assert.Equal(255, color3.G);
            Assert.Equal(255, color3.R);
            Assert.Equal(255, color3.A);

            Bgra color4 = new Bgra("#FF0000");
            Assert.Equal(0, color4.B);
            Assert.Equal(0, color4.G);
            Assert.Equal(255, color4.R);
            Assert.Equal(255, color4.A);
        }

        /// <summary>
        /// Tests to see that in the input hex matches that of the output.
        /// </summary>
        [Fact]
        public void ConvertHex()
        {
            const string First = "FF000000";
            string second = new Bgra(0, 0, 0, 255).BGRA.ToString("X");
            Assert.Equal(First, second);
        }
    }
}
