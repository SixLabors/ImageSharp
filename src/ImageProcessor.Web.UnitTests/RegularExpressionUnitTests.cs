// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RegularExpressionUnitTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Unit tests for the ImageProcessor regular expressions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using ImageProcessor.Imaging;
    using ImageProcessor.Imaging.Filters.Photo;
    using ImageProcessor.Imaging.Formats;
    using ImageProcessor.Plugins.WebP.Imaging.Formats;

    using NUnit.Framework;

    /// <summary>
    /// Test harness for the regular expressions
    /// </summary>
    [TestFixture]
    public class RegularExpressionUnitTests
    {
        /// <summary>
        /// The alpha regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("alpha=66", 66)]
        [TestCase("alpha=101", 100)]
        [TestCase("alpha=000053", 53)]
        public void TestAlphaRegex(string input, int expected)
        {
            Processors.Alpha alpha = new Processors.Alpha();
            alpha.MatchRegexIndex(input);
            int result = alpha.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The contrast regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("brightness=56", 56)]
        [TestCase("brightness=84", 84)]
        [TestCase("brightness=66", 66)]
        [TestCase("brightness=101", 100)]
        [TestCase("brightness=00001", 1)]
        [TestCase("brightness=-50", -50)]
        [TestCase("brightness=0", 0)]
        public void TestBrightnesstRegex(string input, int expected)
        {
            Processors.Brightness brightness = new Processors.Brightness();
            brightness.MatchRegexIndex(input);
            int result = brightness.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The contrast regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("contrast=56", 56)]
        [TestCase("contrast=84", 84)]
        [TestCase("contrast=66", 66)]
        [TestCase("contrast=101", 100)]
        [TestCase("contrast=00001", 1)]
        [TestCase("contrast=-50", -50)]
        [TestCase("contrast=0", 0)]
        public void TestContrastRegex(string input, int expected)
        {
            Processors.Contrast contrast = new Processors.Contrast();
            contrast.MatchRegexIndex(input);
            int result = contrast.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        [Test]
        public void TestCropRegex()
        {
            Dictionary<string, CropLayer> data = new Dictionary<string, CropLayer>
            {
                {
                    "crop=0,0,150,300", new CropLayer(0, 0, 150, 300, CropMode.Pixels)
                },
                {
                    "crop=0.1,0.1,.2,.2&cropmode=percentage", new CropLayer(0.1f, 0.1f, 0.2f, 0.2f, CropMode.Percentage)
                }
            };

            Processors.Crop crop = new Processors.Crop();

            foreach (KeyValuePair<string, CropLayer> item in data)
            {
                crop.MatchRegexIndex(item.Key);
                CropLayer result = crop.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// The filter regex unit test.
        /// </summary>
        [Test]

        public void TestFilterRegex()
        {
            Dictionary<string, IMatrixFilter> data = new Dictionary<string, IMatrixFilter>
            {
                {
                    "filter=lomograph", MatrixFilters.Lomograph
                },
                {
                    "filter=polaroid", MatrixFilters.Polaroid
                },
                {
                    "filter=comic", MatrixFilters.Comic
                },
                {
                    "filter=greyscale", MatrixFilters.GreyScale
                },
                {
                    "filter=blackwhite", MatrixFilters.BlackWhite
                },
                {
                    "filter=invert", MatrixFilters.Invert
                },
                {
                    "filter=gotham", MatrixFilters.Gotham
                },
                {
                    "filter=hisatch", MatrixFilters.HiSatch
                },
                {
                    "filter=losatch", MatrixFilters.LoSatch
                },
                {
                    "filter=sepia", MatrixFilters.Sepia
                }
            };

            Processors.Filter filter = new Processors.Filter();
            foreach (KeyValuePair<string, IMatrixFilter> item in data)
            {
                filter.MatchRegexIndex(item.Key);
                IMatrixFilter result = filter.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// The format regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input querystring.
        /// </param>
        /// <param name="expected">
        /// The expected type.
        /// </param>
        [Test]
        [TestCase("format=bmp", typeof(BitmapFormat))]
        [TestCase("format=png", typeof(PngFormat))]
        [TestCase("format=png8", typeof(PngFormat))]
        [TestCase("format=jpeg", typeof(JpegFormat))]
        [TestCase("format=jpg", typeof(JpegFormat))]
        [TestCase("format=gif", typeof(GifFormat))]
        [TestCase("format=webp", typeof(WebPFormat))]
        public void TestFormatRegex(string input, Type expected)
        {
            Processors.Format format = new Processors.Format();
            format.MatchRegexIndex(input);
            Type result = format.Processor.DynamicParameter.GetType();

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The quality regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("quality=56", 56)]
        [TestCase("quality=84", 84)]
        [TestCase("quality=66", 66)]
        [TestCase("quality=101", 100)]
        [TestCase("quality=00001", 1)]
        [TestCase("quality=0", 0)]
        public void TestQualityRegex(string input, int expected)
        {
            Processors.Quality quality = new Processors.Quality();
            quality.MatchRegexIndex(input);
            int result = quality.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The meta regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("meta=true", true)]
        [TestCase("meta=false", false)]
        public void TestMetaRegex(string input, bool expected)
        {
            Processors.Meta meta = new Processors.Meta();
            meta.MatchRegexIndex(input);
            bool result = meta.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The resize regex unit test.
        /// </summary>
        [Test]
        public void TestResizeRegex()
        {
            Dictionary<string, ResizeLayer> data = new Dictionary<string, ResizeLayer>
            {
                {
                    "width=300", new ResizeLayer(new Size(300, 0))
                },
                {
                    "height=300", new ResizeLayer(new Size(0, 300))
                },
                {
                    "height=300.6", new ResizeLayer(new Size(0, 301))
                },
                {
                    "height=300&mode=crop", new ResizeLayer(new Size(0, 300), ResizeMode.Crop)
                },
                {
                    "width=300&mode=crop", new ResizeLayer(new Size(300, 0), ResizeMode.Crop)
                },
                {
                    "width=300.2&mode=crop", new ResizeLayer(new Size(300, 0), ResizeMode.Crop)
                },
                {
                    "width=600&heightratio=0.416", new ResizeLayer(new Size(600, 250))
                },
                {
                    "width=600&height=250&mode=max", new ResizeLayer(new Size(600, 250), ResizeMode.Max)
                }
            };

            Processors.Resize resize = new Processors.Resize();
            foreach (KeyValuePair<string, ResizeLayer> item in data)
            {
                resize.MatchRegexIndex(item.Key);
                ResizeLayer result = resize.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// The rotate regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("rotate=0", 0F)]
        [TestCase("rotate=270", 270F)]
        [TestCase("rotate=-270", -270F)]
        [TestCase("rotate=28", 28F)]
        [TestCase("rotate=28.7", 28.7F)]
        public void TestRotateRegex(string input, float expected)
        {
            Processors.Rotate rotate = new Processors.Rotate();
            rotate.MatchRegexIndex(input);

            float result = rotate.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The rounded corners regex unit test.
        /// </summary>
        [Test]
        public void TestRoundedCornersRegex()
        {
            Dictionary<string, RoundedCornerLayer> data = new Dictionary<string, RoundedCornerLayer>
            {
                {
                    "roundedcorners=30", new RoundedCornerLayer(30)
                },
                {
                    "roundedcorners=26&tl=true&tr=false&bl=true&br=false", new RoundedCornerLayer(26, true, false, true, false)
                }
            };

            Processors.RoundedCorners roundedCorners = new Processors.RoundedCorners();
            foreach (KeyValuePair<string, RoundedCornerLayer> item in data)
            {
                roundedCorners.MatchRegexIndex(item.Key);
                RoundedCornerLayer result = roundedCorners.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// The saturation regex unit test.
        /// </summary>
        /// <param name="input">
        /// The input string.
        /// </param>
        /// <param name="expected">
        /// The expected result.
        /// </param>
        [Test]
        [TestCase("saturation=56", 56)]
        [TestCase("saturation=84", 84)]
        [TestCase("saturation=66", 66)]
        [TestCase("saturation=101", 100)]
        [TestCase("saturation=00001", 1)]
        [TestCase("saturation=-50", -50)]
        [TestCase("saturation=0", 0)]
        public void TestSaturationRegex(string input, int expected)
        {
            Processors.Saturation saturation = new Processors.Saturation();
            saturation.MatchRegexIndex(input);
            int result = saturation.Processor.DynamicParameter;

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// The tint regex unit test.
        /// </summary>
        [Test]
        public void TestTintRegex()
        {
            Dictionary<string, Color> data = new Dictionary<string, Color>
            {
                {
                    "tint=6aa6cc", ColorTranslator.FromHtml("#" + "6aa6cc")
                },
                {
                    "tint=106,166,204,255", Color.FromArgb(255, 106, 166, 204)
                },
                {
                    "tint=fff", Color.FromArgb(255, 255, 255, 255)
                },
                {
                    "tint=white", Color.White
                }
            };

            Processors.Tint tint = new Processors.Tint();
            foreach (KeyValuePair<string, Color> item in data)
            {
                tint.MatchRegexIndex(item.Key);
                Color result = tint.Processor.DynamicParameter;
                Assert.AreEqual(item.Value.ToArgb(), result.ToArgb());
            }
        }

        /// <summary>
        /// The vignette regex unit test.
        /// </summary>
        [Test]
        public void TestVignetteRegex()
        {
            Dictionary<string, Color> data = new Dictionary<string, Color>
            {
                {
                    "vignette", Color.Black
                },
                {
                    "vignette=true", Color.Black
                },
                {
                    "vignette=6aa6cc", ColorTranslator.FromHtml("#" + "6aa6cc")
                },
                {
                    "vignette=106,166,204,255", Color.FromArgb(255, 106, 166, 204)
                },
                {
                    "vignette=fff", Color.FromArgb(255, 255, 255, 255)
                },
                {
                    "vignette=white", Color.White
                }
            };

            Processors.Vignette vignette = new Processors.Vignette();
            foreach (KeyValuePair<string, Color> item in data)
            {
                vignette.MatchRegexIndex(item.Key);
                Color result = vignette.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }

        /// <summary>
        /// The watermark regex unit test.
        /// </summary>
        [Test]
        public void TestWaterMarkRegex()
        {
            Dictionary<string, TextLayer> data = new Dictionary<string, TextLayer>
            {
                {
                    "watermark=watermark goodness&color=fff&fontsize=36&fontstyle=italic&fontopacity=80&textposition=30,150&dropshadow=true&fontfamily=arial", 
                    new TextLayer
                        {
                            Text = "watermark goodness", 
                            FontColor = ColorTranslator.FromHtml("#" + "ffffff"), 
                            FontSize = 36,
                            Style = FontStyle.Italic,
                            Opacity = 80, 
                            Position = new Point(30, 150),
                            DropShadow = true, 
                            FontFamily = new FontFamily("arial") 
                        }
                },
                {
                    "watermark=لا أحد يحب الألم بذاته، يسعى ورائه أو يبتغيه، ببساطة لأنه الألم&color=fff&fontsize=36&fontstyle=italic&fontopacity=80&textposition=30,150&dropshadow=true&fontfamily=arial&vertical=true&rtl=true", 
                    new TextLayer
                        {
                            Text = "لا أحد يحب الألم بذاته، يسعى ورائه أو يبتغيه، ببساطة لأنه الألم", 
                            FontColor = ColorTranslator.FromHtml("#" + "ffffff"), 
                            FontSize = 36,
                            Style = FontStyle.Italic,
                            Opacity = 80, 
                            Position = new Point(30, 150),
                            DropShadow = true, 
                            FontFamily = new FontFamily("arial"),
                            Vertical = true,
                            RightToLeft = true
                        }
                }
            };

            Processors.Watermark watermark = new Processors.Watermark();
            foreach (KeyValuePair<string, TextLayer> item in data)
            {
                watermark.MatchRegexIndex(item.Key);
                TextLayer result = watermark.Processor.DynamicParameter;
                Assert.AreEqual(item.Value, result);
            }
        }
    }
}