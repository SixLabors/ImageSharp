// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFactoryUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Unit tests for the ImageFactory (loading of images)
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the image factory
    /// </summary>
    [TestFixture]
    public class ImageFactoryUnitTests
    {
        /// <summary>
        /// The path to the binary's folder
        /// </summary>
        private readonly string localPath = Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        /// <summary>
        /// Lists the input files in the Images folder
        /// </summary>
        /// <returns>The list of files.</returns>
        private static IEnumerable<string> ListInputFiles()
        {
            return Directory.GetFiles("./Images");
        }

        /// <summary>
        /// Tests the loading of image from a file
        /// </summary>
        [Test]
        public void TestLoadImageFromFile()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    Assert.AreEqual(fileName, imageFactory.ImagePath);
                    Assert.IsNotNull(imageFactory.Image);
                }
            }
         
        }

        /// <summary>>
        /// Tests the loading of image from a memory stream
        /// </summary>
        [Test]
        public void TestLoadImageFromMemory()
        {
            foreach (var fileName in ListInputFiles())
            {
                byte[] photoBytes = File.ReadAllBytes(fileName);

                using (var inStream = new MemoryStream(photoBytes))
                {
                    using (var imageFactory = new ImageFactory())
                    {
                        imageFactory.Load(inStream);
                        Assert.AreEqual(null, imageFactory.ImagePath);
                        Assert.IsNotNull(imageFactory.Image);
                    }
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void ApplyEffectAlpha()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Alpha(50);
                    var modified = imageFactory.Image.Clone();
                    Assert.AreNotEqual(original, modified);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void ApplyEffectBrightness()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Brightness(50);
                    var modified = imageFactory.Image.Clone();
                    Assert.AreNotEqual(original, modified);
                }
            }
        }
    }
}