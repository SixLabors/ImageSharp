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
    using System.Linq;
    using System.IO;
    using NUnit.Framework;
    using FluentAssertions;

    /// <summary>
    /// Provides helpers for asserting
    /// </summary>
    public class AssertionHelpers
    {
        /// <summary>
        /// Asserts that two images are identical
        /// </summary>
        /// <param name="expected">The expected result</param>
        /// <param name="tested">The tested image</param>
        public static void AssertImagesAreIdentical(Image expected, Image tested, string because, params string[] becauseArgs)
        {
            ToByteArray(expected).SequenceEqual(ToByteArray(tested)).Should().BeTrue(because, becauseArgs);
        }

        /// <summary>
        /// Asserts that two images are different
        /// </summary>
        /// <param name="expected">The not-expected result</param>
        /// <param name="tested">The tested image</param>
        public static void AssertImagesAreDifferent(Image expected, Image tested, string because, params string[] becauseArgs)
        {
            ToByteArray(expected).SequenceEqual(ToByteArray(tested)).Should().BeFalse(because, becauseArgs);
        }

        /// <summary>
        /// Converts an image to a byte array
        /// </summary>
        /// <param name="imageIn">The image to convert</param>
        /// <param name="format">The format to use</param>
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