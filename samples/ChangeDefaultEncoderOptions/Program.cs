// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ChangeDefaultEncoderOptions
{
    class Program
    {
        static void Main(string[] args)
        {
            // lets switch out the default encoder for jpeg to one 
            // that saves at 90 quality and ignores the matadata
            Configuration.Default.SetEncoder(ImageFormats.Jpeg, new JpegEncoder()
            {
                Quality = 90,
                IgnoreMetadata = true
            });
        }
    }
}