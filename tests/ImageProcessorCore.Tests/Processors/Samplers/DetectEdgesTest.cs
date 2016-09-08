// <copyright file="DetectEdgesTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class DetectEdgesTest : FileTestBase
    {
        public static readonly TheoryData<EdgeDetection> DetectEdgesFilters
        = new TheoryData<EdgeDetection>
        {
            EdgeDetection.Kayyali,
            EdgeDetection.Kirsch,
            EdgeDetection.Lapacian3X3,
            EdgeDetection.Lapacian5X5,
            EdgeDetection.LaplacianOfGaussian,
            EdgeDetection.Prewitt,
            EdgeDetection.RobertsCross,
            EdgeDetection.Scharr,
            EdgeDetection.Sobel,
        };

        [Theory]
        [MemberData("DetectEdgesFilters")]
        public void ImageShouldApplyDetectEdgesFilter(EdgeDetection detector)
        {
            const string path = "TestOutput/DetectEdges";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + detector + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.DetectEdges(detector)
                             .Save(output);
                    }
                }
            }
        }

        [Theory]
        [MemberData("DetectEdgesFilters")]
        public void ImageShouldApplyDetectEdgesFilterInBox(EdgeDetection detector)
        {
            const string path = "TestOutput/DetectEdges";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + detector + "-InBox" + Path.GetExtension(file);
                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.DetectEdges(detector, new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2))
                             .Save(output);
                    }
                }
            }
        }
    }
}