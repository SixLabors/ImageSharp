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
    using System.Collections.Generic;
    using System.IO;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the image factory
    /// </summary>
    [TestFixture]
    public class ImageFactoryUnitTests
    {
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

        /// <summary>
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
        /// Tests that the save method actually saves a file
        /// </summary>
        [Test]
        public void TestSaveToDisk()
        {
            foreach (var fileName in ListInputFiles())
            {
                var outputFileName = string.Format("./output/{0}", Path.GetFileName(fileName));
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    imageFactory.Save(outputFileName);
                    Assert.AreEqual(true, File.Exists(outputFileName));
                }
            }
        }

        /// <summary>
        /// Tests that the save method actually writes to memory
        /// </summary>
        [Test]
        public void TestSaveToMemory()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    using (var s = new MemoryStream())
                    {
                        imageFactory.Save(s);
                        s.Seek(0, SeekOrigin.Begin);
                        Assert.AreEqual(true, s.Capacity > 0);
                    }
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectAlpha()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Alpha(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBrightness()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Brightness(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectContrast()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Contrast(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSaturation()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Saturation(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectTint()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Tint(System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.AliceBlue));
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectVignette()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Vignette(System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.AliceBlue));
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectWatermark()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Watermark(new Imaging.TextLayer
                        {
                            Font = "Arial",
                            FontSize = 10,
                            Position = new System.Drawing.Point(10, 10),
                            Text = "Lorem ipsum dolor"
                        });
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBlur()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.GaussianBlur(5);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBlurWithLayer()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.GaussianBlur(new Imaging.GaussianLayer { Sigma = 10, Size = 5, Threshold = 2 });
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSharpen()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.GaussianSharpen(5);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSharpenWithLayer()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.GaussianSharpen(new Imaging.GaussianLayer { Sigma = 10, Size = 5, Threshold = 2 });
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that all filters can be applied
        /// </summary>
        [Test]
        public void TestApplyEffectFilter()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.BlackWhite);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Comic);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Gotham);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.GreyScale);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.HiSatch);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Invert);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Lomograph);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.LoSatch);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Polaroid);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();

                    imageFactory.Filter(Imaging.Filters.MatrixFilters.Sepia);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    imageFactory.Reset();
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestRoundedCorners()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.RoundedCorners(new Imaging.RoundedCornerLayer(5, true, true, true, true));
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that the image is well resized using constraints
        /// </summary>
        [Test]
        public void TestResizeConstraints()
        {
            const int MaxSize = 200;
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    imageFactory.Constrain(new System.Drawing.Size(MaxSize, MaxSize));
                    Assert.LessOrEqual(imageFactory.Image.Width, MaxSize);
                    Assert.LessOrEqual(imageFactory.Image.Height, MaxSize);
                }
            }
        }

        /// <summary>
        /// Tests that the image is well cropped
        /// </summary>
        [Test]
        public void TestCrop()
        {
            const int MaxSize = 20;
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Crop(new System.Drawing.Rectangle(0, 0, MaxSize, MaxSize));
                    Assert.AreNotEqual(original, imageFactory.Image);
                    Assert.AreEqual(MaxSize, imageFactory.Image.Width);
                    Assert.LessOrEqual(MaxSize, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Tests that the image is well cropped
        /// </summary>
        [Test]
        public void TestCropWithCropLayer()
        {
            const int MaxSize = 20;
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = imageFactory.Image.Clone();
                    imageFactory.Crop(new Imaging.CropLayer(0, 0, MaxSize, MaxSize, Imaging.CropMode.Pixels));
                    Assert.AreNotEqual(original, imageFactory.Image);
                    Assert.AreEqual(MaxSize, imageFactory.Image.Width);
                    Assert.LessOrEqual(MaxSize, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Tests that the image is flipped
        /// </summary>
        [Test]
        public void TestFlip()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = (System.Drawing.Image)imageFactory.Image.Clone();
                    imageFactory.Flip(true);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    Assert.AreEqual(original.Width, imageFactory.Image.Width);
                    Assert.AreEqual(original.Height, imageFactory.Image.Height);
                    imageFactory.Reset();

                    imageFactory.Flip(false);
                    Assert.AreNotEqual(original, imageFactory.Image);
                    Assert.AreEqual(original.Width, imageFactory.Image.Width);
                    Assert.AreEqual(original.Height, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestResize()
        {
            const int NewSize = 150;
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    imageFactory.Resize(new System.Drawing.Size(NewSize, NewSize));
                    Assert.AreEqual(NewSize, imageFactory.Image.Width);
                    Assert.AreEqual(NewSize, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestResizeWithLayer()
        {
            const int NewSize = 150;
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    imageFactory.Resize(new Imaging.ResizeLayer(new System.Drawing.Size(NewSize, NewSize), Imaging.ResizeMode.Stretch, Imaging.AnchorPosition.Left));
                    Assert.AreEqual(NewSize, imageFactory.Image.Width);
                    Assert.AreEqual(NewSize, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestRotate()
        {
            foreach (var fileName in ListInputFiles())
            {
                using (var imageFactory = new ImageFactory())
                {
                    imageFactory.Load(fileName);
                    var original = (System.Drawing.Image)imageFactory.Image.Clone();
                    imageFactory.Rotate(90);
                    Assert.AreEqual(original.Height, imageFactory.Image.Width);
                    Assert.AreEqual(original.Width, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Lists the input files in the Images folder
        /// </summary>
        /// <returns>The list of files.</returns>
        private static IEnumerable<string> ListInputFiles()
        {
            return Directory.GetFiles("./Images");
        }
    }
}