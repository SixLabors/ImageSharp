// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Tuples;
using static SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters.JpegColorConverter;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.HwIntrinsics_SSE_AVX))]
    public class Vector4OctetPack
    {
        private static Vector4Pair r = new Vector4Pair
        {
            A = new Vector4(1, 2, 3, 4),
            B = new Vector4(5, 6, 7, 8)
        };

        private static Vector4Pair g = new Vector4Pair
        {
            A = new Vector4(9, 10, 11, 12),
            B = new Vector4(13, 14, 15, 16)
        };

        private static Vector4Pair b = new Vector4Pair
        {
            A = new Vector4(17, 18, 19, 20),
            B = new Vector4(21, 22, 23, 24)
        };

        [Benchmark]
        public void Pack()
        {
            Vector4Octet v = default;

            v.Pack(ref r, ref g, ref b);
        }
    }
}
