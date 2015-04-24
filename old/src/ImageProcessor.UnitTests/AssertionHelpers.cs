// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AssertionHelpers.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    using FluentAssertions;

    /// <summary>
    /// Provides helpers for asserting
    /// </summary>
    public class AssertionHelpers
    {
        /// <summary>
        /// Asserts that two images are identical
        /// </summary>
        /// <param name="expected">
        /// The expected result
        /// </param>
        /// <param name="tested">
        /// The tested image
        /// </param>
        /// <param name="because">
        /// The because message.
        /// </param>
        /// <param name="becauseArgs">
        /// The because arguments.
        /// </param>
        public static void AssertImagesAreIdentical(Image expected, Image tested, string because, params object[] becauseArgs)
        {
            ToByteArray(expected).SequenceEqual(ToByteArray(tested)).Should().BeTrue(because, becauseArgs);
        }

        /// <summary>
        /// Asserts that two images are different
        /// </summary>
        /// <param name="expected">
        /// The not-expected result
        /// </param>
        /// <param name="tested">
        /// The tested image
        /// </param>
        /// <param name="because">
        /// The because message.
        /// </param>
        /// <param name="becauseArgs">
        /// The because arguments.
        /// </param>
        public static void AssertImagesAreDifferent(Image expected, Image tested, string because, params object[] becauseArgs)
        {
            ToByteArray(expected).SequenceEqual(ToByteArray(tested)).Should().BeFalse(because, becauseArgs);
        }

        /// <summary>
        /// Converts an image to a byte array
        /// </summary>
        /// <param name="imageIn">The image to convert</param>
        /// <returns>An array of bytes representing the image</returns>
        public static byte[] ToByteArray(Image imageIn)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                imageIn.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }
    }
}