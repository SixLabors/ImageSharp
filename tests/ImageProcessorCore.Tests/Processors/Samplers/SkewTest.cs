// <copyright file="SkewTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class SkewTest : FileTestBase
    {
        public static readonly TheoryData<float, float> SkewValues
        = new TheoryData<float, float>
        {
            { 20, 10 },
            { -20, -10 }
        };

        [Theory]
        [MemberData("SkewValues")]
        public void ImageShouldApplySkewSampler(float x, float y)
        {
            const string path = "TestOutput/Skew";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // Matches live example 
            // http://www.w3schools.com/css/tryit.asp?filename=trycss3_transform_skew
            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + x + "-" + y + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                    {
                        image.Skew(x, y)
                             .Save(output);
                    }
                }
            }
        }
    }
}