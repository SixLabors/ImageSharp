// <copyright file="DecodePng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using CoreSize = SixLabors.Primitives.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeFilteredPng : BenchmarkBase
    {
        private byte[] filter0;
        private byte[] filter1;
        private byte[] filter2;
        private byte[] filter3;
        private byte[] filter4;

        [GlobalSetup]
        public void ReadImages()
        {
            this.filter0 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter0));
            this.filter1 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter1));
            this.filter2 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter2));
            this.filter3 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter3));
            this.filter4 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter4));
        }

        [Benchmark(Baseline = true, Description = "None-filtered PNG file")]
        public CoreSize PngFilter0()
        {
            return LoadPng(this.filter0);
        }

        [Benchmark(Description = "Sub-filtered PNG file")]
        public CoreSize PngFilter1()
        {
            return LoadPng(this.filter1);
        }

        [Benchmark(Description = "Up-filtered PNG file")]
        public CoreSize PngFilter2()
        {
            return LoadPng(this.filter2);
        }

        [Benchmark(Description = "Average-filtered PNG file")]
        public CoreSize PngFilter3()
        {
            return LoadPng(this.filter3);
        }

        [Benchmark(Description = "Paeth-filtered PNG file")]
        public CoreSize PngFilter4()
        {
            return LoadPng(this.filter4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CoreSize LoadPng(byte[] bytes)
        {
            using (var image = Image.Load<Rgba32>(bytes))
            {
                return image.Size();
            }
        }

        private static string TestImageFullPath(string path) => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path);
    }
}