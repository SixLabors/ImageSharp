
namespace ImageSharp.Tests.Drawing.Paths
{
    using System;
    using System.IO;
    using ImageSharp;
    using Processing;
    using System.Collections.Generic;
    using ImageSharp.Formats;

    /// <summary>
    /// Watches but does not actually run the processors against the image.
    /// </summary>
    /// <seealso cref="ImageSharp.Image{ImageSharp.Color}" />
    public class ProcessorWatchingImage : Image<Color>, IImageCallbacks
    {
        public List<ProcessorDetails> ProcessorApplications { get; } = new List<ProcessorDetails>();
        
        public ProcessorWatchingImage(int width, int height)
           : base(width, height, Configuration.CreateDefaultInstance())
        {
            this.Callbacks = this;
        }

        public bool OnSaving<TColor>(ImageBase<TColor> image, Stream stream, IImageEncoder encoder, IEncoderOptions options) where TColor : struct, IPixel<TColor>
        {
            return true;
        }

        public bool OnProcessing<TColor>(ImageBase<TColor> image, IImageProcessor<TColor> processor, Rectangle rectangle) where TColor : struct, IPixel<TColor>
        {
            this.ProcessorApplications.Add(new ProcessorDetails
            {
                processor = (IImageProcessor<Color>)processor,
                rectangle = rectangle
            });
            return false;// do not really apply the processor to speed up testing
        }

        public struct ProcessorDetails
        {
            public IImageProcessor<Color> processor;
            public Rectangle rectangle;
        }
    }
}
