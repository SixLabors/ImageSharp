// <copyright file="DecodeJpegMultiple.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Collections.Generic;
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Jpeg.GolangPort;
    using ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using CoreImage = ImageSharp.Image;

    [Config(typeof(Config.Short))]
    public class DecodeJpegMultiple : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[]
        {
            "Jpg/"
        };

        protected override IEnumerable<string> SearchPatterns => new[] { "*.jpg" };

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp NEW")]
        public void DecodeJpegImageSharpNwq()
        {
            this.ForEachStream(
                ms => CoreImage.Load<Rgba32>(ms)
                );
        }


        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp Original")]
        public void DecodeJpegImageSharpOriginal()
        {
            this.ForEachStream(
                ms => CoreImage.Load<Rgba32>(ms, new OriginalJpegDecoder())
            );
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public void DecodeJpegSystemDrawing()
        {
            this.ForEachStream(
                System.Drawing.Image.FromStream
                );
        }


        public sealed class OriginalJpegDecoder : IImageDecoder, IJpegDecoderOptions
        {
            /// <summary>
            /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
            /// </summary>
            public bool IgnoreMetadata { get; set; }

            /// <inheritdoc/>
            public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
                where TPixel : struct, IPixel<TPixel>
            {
                Guard.NotNull(stream, "stream");

                using (var decoder = new OrigJpegDecoderCore(configuration, this))
                {
                    return decoder.Decode<TPixel>(stream);
                }
            }
        }
    }
}