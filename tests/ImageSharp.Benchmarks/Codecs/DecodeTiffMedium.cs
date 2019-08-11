// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeTiffMedium : DecodeTiffBase
    {
        protected override string FileNamePrefix => "jpeg444_medium";
    }
}
