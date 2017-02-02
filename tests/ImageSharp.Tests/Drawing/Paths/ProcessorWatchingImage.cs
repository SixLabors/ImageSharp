
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using System.IO;
    using ImageSharp;
    using Processing;
    using System.Collections.Generic;

    /// <summary>
    /// Watches but does not actually run the processors against the image.
    /// </summary>
    /// <seealso cref="ImageSharp.Image{ImageSharp.Color}" />
    public class ProcessorWatchingImage : Image<Color>
    {
        public List<ProcessorDetails> ProcessorApplications { get; } = new List<ProcessorDetails>();
        
        public ProcessorWatchingImage(int width, int height)
           : base(width, height, Configuration.CreateDefaultInstance())
        {
        }

        public override void ApplyProcessor(IImageProcessor<Color> processor, Rectangle rectangle)
        {
            ProcessorApplications.Add(new ProcessorDetails
            {
                processor = processor,
                rectangle = rectangle
            });
        }

        public struct ProcessorDetails
        {
            public IImageProcessor<Color> processor;
            public Rectangle rectangle;
        }
    }
}
