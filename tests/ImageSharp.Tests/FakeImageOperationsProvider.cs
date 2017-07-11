namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    public class FakeImageOperationsProvider : IImageOperationsProvider
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

        public IImageOperations<TPixel> CreateMutator<TPixel>(Image<TPixel> source) where TPixel : struct, IPixel<TPixel>
        {
            var op = new FakeImageOperations<TPixel>(source);
            this.ImageOperators.Add(op);
            return op;
        }


        public class FakeImageOperations<TPixel> : IImageOperations<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            public Image<TPixel> source;

            public List<AppliedOpperation> applied = new List<AppliedOpperation>();

            public FakeImageOperations(Image<TPixel> source)
            {
                this.source = source;
            }

            public IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
            {
                applied.Add(new AppliedOpperation
                {
                    Processor = processor,
                    Rectangle = rectangle
                });
                return this;
            }

            public IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor)
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
