// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Tests.Processing
{
    internal class FakeImageOperationsProvider : IImageProcessingContextFactory
    {
        private readonly List<object> imageOperators = new List<object>();

        public bool HasCreated<TPixel>(Image<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return this.Created(source).Any();
        }

        public IEnumerable<FakeImageOperations<TPixel>> Created<TPixel>(Image<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return this.imageOperators.OfType<FakeImageOperations<TPixel>>()
                .Where(x => x.Source == source);
        }

        public IEnumerable<FakeImageOperations<TPixel>.AppliedOperation> AppliedOperations<TPixel>(Image<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return this.Created(source)
                .SelectMany(x => x.Applied);
        }

        public IInternalImageProcessingContext<TPixel> CreateImageProcessingContext<TPixel>(Configuration configuration, Image<TPixel> source, bool mutate)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var op = new FakeImageOperations<TPixel>(configuration, source, mutate);
            this.imageOperators.Add(op);
            return op;
        }

        public class FakeImageOperations<TPixel> : IInternalImageProcessingContext<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>
        {
            public FakeImageOperations(Configuration configuration, Image<TPixel> source, bool mutate)
            {
                this.Configuration = configuration;
                this.Source = mutate ? source : source?.Clone();
            }

            public Image<TPixel> Source { get; }

            public List<AppliedOperation> Applied { get; } = new List<AppliedOperation>();

            public Configuration Configuration { get; }

            public IDictionary<object, object> Properties { get; } = new ConcurrentDictionary<object, object>();

            public Image<TPixel> GetResultImage()
            {
                return this.Source;
            }

            public Size GetCurrentSize()
            {
                return this.Source.Size();
            }

            public IImageProcessingContext ApplyProcessor(IImageProcessor processor, Rectangle rectangle)
            {
                this.Applied.Add(new AppliedOperation
                {
                    Rectangle = rectangle,
                    NonGenericProcessor = processor
                });
                return this;
            }

            public IImageProcessingContext ApplyProcessor(IImageProcessor processor)
            {
                this.Applied.Add(new AppliedOperation
                {
                    NonGenericProcessor = processor
                });
                return this;
            }

            public struct AppliedOperation
            {
                public Rectangle? Rectangle { get; set; }

                public IImageProcessor<TPixel> GenericProcessor { get; set; }

                public IImageProcessor NonGenericProcessor { get; set; }
            }
        }
    }
}
