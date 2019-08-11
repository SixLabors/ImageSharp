//Copyright(c) Six Labors and contributors.
//Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.LongClr))]
    public class DecodeTiffBig : DecodeTiffBase
    {
        protected override string FileNamePrefix => "jpeg444_big";
    }
}
