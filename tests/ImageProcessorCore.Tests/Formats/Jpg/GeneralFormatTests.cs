// <copyright file="GeneralFormatTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class GeneralFormatTests : FileTestBase
    {
        [Fact]
        public void ResolutionShouldChange()
        {
            if (!Directory.Exists("TestOutput/Resolution"))
            {
                Directory.CreateDirectory("TestOutput/Resolution");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Resolution/{filename}"))
                    {
                        image.VerticalResolution = 150;
                        image.HorizontalResolution = 150;
                        image.Save(output);
                    }
                }
            }
        }
    }
}