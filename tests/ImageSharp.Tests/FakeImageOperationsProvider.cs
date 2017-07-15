namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    public class FakeImageOperationsProvider : IImageProcessorApplicatorFactory
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
                .Where(x => x.source == source);
        }

        public IEnumerable<FakeImageOperations<TPixel>.AppliedOpperation> AppliedOperations<TPixel>(Image<TPixel> source) where TPixel : struct, IPixel<TPixel>
        {
            return Created(source)
                .SelectMany(x => x.applied);
        }

        public IInternalImageProcessorApplicator<TPixel> CreateImageOperations<TPixel>(Image<TPixel> source, bool mutate) where TPixel : struct, IPixel<TPixel>
        {
            var op = new FakeImageOperations<TPixel>(source, mutate);
            this.ImageOperators.Add(op);
            return op;
        }


        public class FakeImageOperations<TPixel> : IInternalImageProcessorApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            public Image<TPixel> source;

            public List<AppliedOpperation> applied = new List<AppliedOpperation>();
            public bool mutate;

            public FakeImageOperations(Image<TPixel> source, bool mutate)
            {
                this.mutate = mutate;
                if (mutate)
                {
                    this.source = source;
                }
                else
                {
                    this.source = source?.Clone();
                }
            }

            public Image<TPixel> Apply()
            {
                return source;
            }

            public IImageProcessorApplicator<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
            {
                applied.Add(new AppliedOpperation
                {
                    Processor = processor,
                    Rectangle = rectangle
                });
                return this;
            }

            public IImageProcessorApplicator<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
            {
                applied.Add(new AppliedOpperation
                {
                    Processor = processor
                });
                return this;
            }
            public struct AppliedOpperation
            {
                public Rectangle? Rectangle { get; set; }
                public IImageProcessor<TPixel> Processor { get; set; }
            }
        }
    }
}
