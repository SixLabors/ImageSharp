// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests
{
    internal class FakeImageOperationsProvider : IImageProcessingContextFactory
    {
        private List<object> ImageOperators = new List<object>();

        public bool HasCreated<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Created(source).Any();
        }
        public IEnumerable<FakeImageOperations<TPixel>> Created<TPixel>(Image<TPixel> source) where TPixel : struct, IPixel<TPixel>
        {
            return this.ImageOperators.OfType<FakeImageOperations<TPixel>>()
                .Where(x => x.Source == source);
        }

        public IEnumerable<FakeImageOperations<TPixel>.AppliedOperation> AppliedOperations<TPixel>(Image<TPixel> source) where TPixel : struct, IPixel<TPixel>
        {
            return Created(source)
                .SelectMany(x => x.Applied);
        }

        public IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Image<TPixel> source, bool mutate, Configuration configuration) where TPixel : struct, IPixel<TPixel>
        {
            var op = new FakeImageOperations<TPixel>(source, mutate, configuration);
            this.ImageOperators.Add(op);
            return op;
        }

        public class FakeImageOperations<TPixel> : IInternalImageProcessingContext<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            private bool mutate;

            public FakeImageOperations(Image<TPixel> source, bool mutate, Configuration configuration = null)
            {
                this.mutate = mutate;
                this.Source = mutate ? source : source?.Clone();
                this.Configuration = configuration ?? this.Source.GetConfiguration();
            }

            public Image<TPixel> Source { get; }

            public List<AppliedOperation> Applied { get; } = new List<AppliedOperation>();

            public Configuration Configuration { get; }

            public Image<TPixel> Apply()
            {
                return this.Source;
            }

            public Size GetCurrentSize()
            {
                return this.Source.Size();
            }

            public IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
            {
                this.Applied.Add(new AppliedOperation
                {
                    Processor = processor,
                    Rectangle = rectangle,
                    Configuration = this.Configuration
                });
                return this;
            }

            public IImageProcessingContext<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
            {
                this.Applied.Add(new AppliedOperation
                {
                    Processor = processor,
                    Configuration = this.Configuration
                });
                return this;
            }

            public struct AppliedOperation
            {
                public Rectangle? Rectangle { get; set; }
                public IImageProcessor<TPixel> Processor { get; set; }
                public Configuration Configuration { get; set; }
            }
        }
    }
}
