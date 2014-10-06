// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFactoryUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace ImageProcessor.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.EdgeDetection;
    using ImageProcessor.Imaging.Filters.Photo;

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
        private IEnumerable<FileInfo> imagesInfos;

        /// <summary>
        /// The list of ImageFactories. Designed to speed up the test a bit more.
        /// </summary>
        private List<ImageFactory> imagesFactories;

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
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                using (MemoryStream s = new MemoryStream())
                {
                    imageFactory.Save(s);
                    s.Seek(0, SeekOrigin.Begin);
                    Assert.AreEqual(true, s.Capacity > 0);
                }
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectAlpha()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Alpha(50);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that brightness changes is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBrightness()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Brightness(50);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that background color changes are really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBackgroundColor()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.BackgroundColor(Color.Yellow);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a contrast change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectContrast()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Contrast(50);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a saturation change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSaturation()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Saturation(50);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a tint change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectTint()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Tint(Color.FromKnownColor(KnownColor.AliceBlue));
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a vignette change is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectVignette()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Vignette(Color.FromKnownColor(KnownColor.AliceBlue));
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectWatermark()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Watermark(new TextLayer
                {
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 10,
                    Position = new Point(10, 10),
                    Text = "Lorem ipsum dolor"
                });
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBlur()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.GaussianBlur(5);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectBlurWithLayer()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.GaussianBlur(new GaussianLayer { Sigma = 10, Size = 5, Threshold = 2 });
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSharpen()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.GaussianSharpen(5);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestApplyEffectSharpenWithLayer()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.GaussianSharpen(new GaussianLayer { Sigma = 10, Size = 5, Threshold = 2 });
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that all filters can be applied
        /// </summary>
        [Test]
        public void TestApplyEffectFilter()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.BlackWhite);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Comic);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Gotham);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.GreyScale);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.HiSatch);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Invert);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Lomograph);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.LoSatch);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Polaroid);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Filter(MatrixFilters.Sepia);
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that a filter is really applied by checking that the image is modified
        /// </summary>
        [Test]
        public void TestRoundedCorners()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.RoundedCorners(new RoundedCornerLayer(5));
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that the image is well resized using constraints
        /// </summary>
        [Test]
        public void TestResizeConstraints()
        {
            const int MaxSize = 200;
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                imageFactory.Constrain(new Size(MaxSize, MaxSize));
                Assert.LessOrEqual(imageFactory.Image.Width, MaxSize);
                Assert.LessOrEqual(imageFactory.Image.Height, MaxSize);
            }
        }

        /// <summary>
        /// Tests that the image is well cropped
        /// </summary>
        [Test]
        public void TestCrop()
        {
            const int MaxSize = 20;
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Crop(new Rectangle(0, 0, MaxSize, MaxSize));
                AssertImagesAreDifferent(original, imageFactory.Image);
                Assert.AreEqual(MaxSize, imageFactory.Image.Width);
                Assert.LessOrEqual(MaxSize, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the image is well cropped
        /// </summary>
        [Test]
        public void TestCropWithCropLayer()
        {
            const int MaxSize = 20;
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Crop(new CropLayer(0, 0, MaxSize, MaxSize, CropMode.Pixels));
                AssertImagesAreDifferent(original, imageFactory.Image);
                Assert.AreEqual(MaxSize, imageFactory.Image.Width);
                Assert.LessOrEqual(MaxSize, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the image is flipped
        /// </summary>
        [Test]
        public void TestFlip()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Flip(true);
                AssertImagesAreDifferent(original, imageFactory.Image);
                Assert.AreEqual(original.Width, imageFactory.Image.Width);
                Assert.AreEqual(original.Height, imageFactory.Image.Height);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Flip();
                AssertImagesAreDifferent(original, imageFactory.Image);
                Assert.AreEqual(original.Width, imageFactory.Image.Width);
                Assert.AreEqual(original.Height, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestResize()
        {
            const int NewSize = 150;
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                imageFactory.Resize(new Size(NewSize, NewSize));
                Assert.AreEqual(NewSize, imageFactory.Image.Width);
                Assert.AreEqual(NewSize, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestResizeWithLayer()
        {
            const int NewSize = 150;
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                imageFactory.Resize(new ResizeLayer(new Size(NewSize, NewSize), ResizeMode.Stretch, AnchorPosition.Left));
                Assert.AreEqual(NewSize, imageFactory.Image.Width);
                Assert.AreEqual(NewSize, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the image is resized
        /// </summary>
        [Test]
        public void TestRotate()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Rotate(90);
                Assert.AreEqual(original.Height, imageFactory.Image.Width);
                Assert.AreEqual(original.Width, imageFactory.Image.Height);
            }
        }

        /// <summary>
        /// Tests that the images hue has been altered.
        /// </summary>
        [Test]
        public void TestHue()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Hue(90);
                AssertImagesAreDifferent(original, imageFactory.Image);

                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.Hue(116, true);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that the image has been pixelated.
        /// </summary>
        [Test]
        public void TestPixelate()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.Pixelate(8);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that the images quality has been set.
        /// </summary>
        [Test]
        public void TestQuality()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                int original = imageFactory.CurrentImageFormat.Quality;
                imageFactory.Quality(69);
                int updated = imageFactory.CurrentImageFormat.Quality;

                Assert.AreNotEqual(original, updated);
            }
        }

        /// <summary>
        /// Tests that the image has had a color replaced.
        /// </summary>
        [Test]
        public void TestReplaceColor()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);
                imageFactory.ReplaceColor(Color.White, Color.Black, 90);
                AssertImagesAreDifferent(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Tests that the various edge detection algorithms are applied.
        /// </summary>
        [Test]
        public void TestEdgeDetection()
        {
            foreach (ImageFactory imageFactory in this.ListInputImages())
            {
                Image original = (Image)imageFactory.Image.Clone();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new KayyaliEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new KirschEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new Laplacian3X3EdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new Laplacian5X5EdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new LaplacianOfGaussianEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new PrewittEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new RobertsCrossEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new ScharrEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);

                imageFactory.DetectEdges(new SobelEdgeFilter());
                AssertImagesAreDifferent(original, imageFactory.Image);
                imageFactory.Reset();
                AssertImagesAreIdentical(original, imageFactory.Image);
            }
        }

        /// <summary>
        /// Gets the files matching the given extensions.
        /// </summary>
        /// <param name="dir">
        /// The <see cref="System.IO.DirectoryInfo"/>.
        /// </param>
        /// <param name="extensions">
        /// The extensions.
        /// </param>
        /// <returns>
        /// A collection of <see cref="System.IO.FileInfo"/>
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// The extensions variable is null.
        /// </exception>
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
            if (this.imagesInfos != null)
            {
                return this.imagesInfos;
            }

            DirectoryInfo directoryInfo = new DirectoryInfo("./Images");

            this.imagesInfos = GetFilesByExtensions(directoryInfo, new[] { ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".bmp", ".webp" });

            return this.imagesInfos;
        }

        /// <summary>
        /// Lists the input images to use from the Images folder
        /// </summary>
        /// <returns>The list of images</returns>
        private IEnumerable<ImageFactory> ListInputImages()
        {
            if (imagesFactories == null || imagesFactories.Count() == 0)
            {
                imagesFactories = new List<ImageFactory>();
                foreach (FileInfo fi in this.ListInputFiles())
                {
                    imagesFactories.Add((new ImageFactory()).Load(fi.FullName));
                }
            }

            return imagesFactories;
        }

        /// <summary>
        /// Asserts that two images are identical
        /// </summary>
        /// <param name="expected">The expected result</param>
        /// <param name="tested">The tested image</param>
        private void AssertImagesAreIdentical(Image expected, Image tested)
        {
            Assert.IsTrue(ToByteArray(expected).SequenceEqual(ToByteArray(tested)));
        }

        /// <summary>
        /// Asserts that two images are different
        /// </summary>
        /// <param name="expected">The not-expected result</param>
        /// <param name="tested">The tested image</param>
        private void AssertImagesAreDifferent(Image expected, Image tested)
        {
            Assert.IsFalse(ToByteArray(expected).SequenceEqual(ToByteArray(tested)));
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