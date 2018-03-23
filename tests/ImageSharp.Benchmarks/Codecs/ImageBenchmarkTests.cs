// <copyright file="ImageBenchmarkTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// This file contains small, cheap and "unit test" benchmarks to test MultiImageBenchmarkBase.
// Need this because there are no real test cases for the common benchmark utility stuff.

// Uncomment this to enable benchmark testing
// #define TEST

#if TEST

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Jobs;

    internal class Assert
    {
        public static void True(bool condition, string meassage)
        {
            if (!condition) throw new Exception(meassage);
        }

        public static void Equal<T>(T a, T b, string message) => True(a.Equals(b), message);

        public static void Equal(object a, object b, string message) => True(a.Equals(b), message);
    }

    public class MultiImageBenchmarkBase_ShouldReadSingleFiles : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/F.bmp", "Jpg/Exif.jpg" };

        [Benchmark]
        public void Run()
        {
            //Console.WriteLine("FileNames2Bytes.Count(): " + this.FileNames2Bytes.Count());
            Assert.Equal(this.FileNames2Bytes.Count(), 2, "MultiImageBenchmarkBase_ShouldReadFiles failed");
        }
    }

    public class MultiImageBenchmarkBase_ShouldReadMixed : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/F.bmp", "Gif/" };

        [Benchmark]
        public void Run()
        {
            //Console.WriteLine("FileNames2Bytes.Count(): " + this.FileNames2Bytes.Count());
            Assert.True(this.FileNames2Bytes.Count() > 2, "MultiImageBenchmarkBase_ShouldReadMixed failed");
            Assert.True(
                this.FileNames2Bytes.Any(kv => kv.Key.Contains("F.bmp")),
                "MultiImageBenchmarkBase_ShouldReadMixed failed"
                );
        }
    }

    [Config(typeof(Config.Short))]
    public class UseCustomConfigTest : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/" };

        [Benchmark]
        public void Run()
        {
            this.ForEachStream(
                ms => new ImageSharp.Image(ms)
                );
        }
    }
}

#endif