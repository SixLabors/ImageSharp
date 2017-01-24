// <copyright file="DecodePng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using ImageSharp;

    public class DecodeFilteredPng : BenchmarkBase
    {
        private MemoryStream filter0;
        private MemoryStream filter1;
        private MemoryStream filter2;
        private MemoryStream filter3;
        private MemoryStream filter4;

        [Setup]
        public void ReadImages()
        {
            this.filter0 = new MemoryStream(File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/filter0.png"));
            this.filter1 = new MemoryStream(File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/filter1.png"));
            this.filter2 = new MemoryStream(File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/filter2.png"));
            this.filter3 = new MemoryStream(File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/filter3.png"));
            this.filter4 = new MemoryStream(File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/filter4.png"));
        }

        private Image LoadPng(MemoryStream stream)
        {
            return new Image(stream);
        }

        [Benchmark(Baseline = true, Description = "None-filtered PNG file")]
        public Image PngFilter0()
        {
            return LoadPng(filter0);
        }

        [Benchmark(Description = "Sub-filtered PNG file")]
        public Image PngFilter1()
        {
            return LoadPng(filter1);
        }

        [Benchmark(Description = "Up-filtered PNG file")]
        public Image PngFilter2()
        {
            return LoadPng(filter2);
        }

        [Benchmark(Description = "Average-filtered PNG file")]
        public Image PngFilter3()
        {
            return LoadPng(filter3);
        }

        [Benchmark(Description = "Paeth-filtered PNG file")]
        public Image PngFilter4()
        {
            return LoadPng(filter4);
        }
    }
}