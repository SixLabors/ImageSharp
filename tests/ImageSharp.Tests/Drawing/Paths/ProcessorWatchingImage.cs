
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using System.IO;
    using ImageSharp;
    using Processing;
    using System.Collections.Generic;
   using ImageSharp.PixelFormats;

    /// <summary>
    /// Watches but does not actually run the processors against the image.
    /// </summary>
    /// <seealso cref="ImageSharp.Image{Rgba32}" />
    public class ProcessorWatchingImage : Image<Rgba32>
    {
        public List<ProcessorDetails> ProcessorApplications { get; } = new List<ProcessorDetails>();
        
        public ProcessorWatchingImage(int width, int height)
           : base(width, height, Configuration.CreateDefaultInstance())
        {
        }

        public override void ApplyProcessor(IImageProcessor<Rgba32> processor, Rectangle rectangle)
        {
            this.ProcessorApplications.Add(new ProcessorDetails
            {
                processor = processor,
                rectangle = rectangle
            });
        }

        public struct ProcessorDetails
        {
            public IImageProcessor<Rgba32> processor;
            public Rectangle rectangle;
        }
    }
}
