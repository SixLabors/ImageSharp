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
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Test harness for the image factory
    /// </summary>
    [TestFixture]
    public class ImageFactoryUnitTests
    {
        /// <summary>
        /// The list of images. Designed to speed up the tests a little.
        /// </summary>
        private IEnumerable<FileInfo> images;

        /// <summary>
        /// Tests the loading of image from a file
        /// </summary>
        [Test]
        public void TestLoadImageFromFile()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Assert.AreEqual(file.FullName, imageFactory.ImagePath);
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                byte[] photoBytes = File.ReadAllBytes(file.FullName);

                using (MemoryStream inStream = new MemoryStream(photoBytes))
                {
                    using (ImageFactory imageFactory = new ImageFactory())
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                string outputFileName = string.Format("./output/{0}", file.Name);
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    using (MemoryStream s = new MemoryStream())
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Alpha(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that brightness changes is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBrightness()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Brightness(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that background color changes are really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBackgroundColor()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.BackgroundColor(Color.Yellow);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a contrast change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectContrast()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Contrast(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a saturation change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSaturation()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Saturation(50);
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a tint change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectTint()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Tint(Color.FromKnownColor(KnownColor.AliceBlue));
                    Assert.AreNotEqual(original, imageFactory.Image);
                }
            }
        }

        /// <summary>
        /// Tests that a vignette change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectVignette()
        {
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Vignette(Color.FromKnownColor(KnownColor.AliceBlue));
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Watermark(new Imaging.TextLayer
                    {
                        Font = "Arial",
                        FontSize = 10,
                        Position = new Point(10, 10),
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();

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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    imageFactory.Constrain(new Size(MaxSize, MaxSize));
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Crop(new Rectangle(0, 0, MaxSize, MaxSize));
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    imageFactory.Resize(new Size(NewSize, NewSize));
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    imageFactory.Resize(new Imaging.ResizeLayer(new Size(NewSize, NewSize), Imaging.ResizeMode.Stretch, Imaging.AnchorPosition.Left));
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
            foreach (FileInfo file in this.ListInputFiles())
            {
                using (ImageFactory imageFactory = new ImageFactory())
                {
                    imageFactory.Load(file.FullName);
                    Image original = (Image)imageFactory.Image.Clone();
                    imageFactory.Rotate(90);
                    Assert.AreEqual(original.Height, imageFactory.Image.Width);
                    Assert.AreEqual(original.Width, imageFactory.Image.Height);
                }
            }
        }

        /// <summary>
        /// Gets the files matching the given extensions.
        /// </summary>
        /// <param name="dir">The <see cref="System.IO.DirectoryInfo"/>.</param>
        /// <param name="extensions">The extensions.</param>
        /// <returns>A collection of <see cref="System.IO.FileInfo"/></returns>
        /// <exception cref="System.ArgumentNullException">The extensions variable is null.</exception>
        private static IEnumerable<FileInfo> GetFilesByExtensions(DirectoryInfo dir, params string[] extensions)
        {
            if (extensions == null)
            {
                throw new ArgumentNullException("extensions");
            }

            IEnumerable<FileInfo> files = dir.EnumerateFiles();
            return files.Where(f => extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Lists the input files in the Images folder
        /// </summary>
        /// <returns>The list of files.</returns>
        private IEnumerable<FileInfo> ListInputFiles()
        {
            if (this.images != null)
            {
                return this.images;
            }

            DirectoryInfo directoryInfo = new DirectoryInfo("./Images");

            this.images = GetFilesByExtensions(directoryInfo, new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".bmp", ".webp" });

            return this.images;
        }
    }
}