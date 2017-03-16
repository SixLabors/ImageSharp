
namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using ImageSharp;
    using Processing;
    using System.Collections.Generic;
    using ImageSharp.Formats;
    using ImageSharp.IO;

    /// <summary>
    /// Watches but does not actually run the processors against the image.
    /// </summary>
    /// <seealso cref="ImageSharp.Image{ImageSharp.Color}" />
    public class SaveWatchingImage : Image<Color>, IImageCallbacks
    {
        public List<OperationDetails> Saves { get; } = new List<OperationDetails>();

        public SaveWatchingImage(int width, int height, IFileSystem fs = null)
           : base(width, height, Configuration.CreateDefaultInstance())
        {
            //switch out the file system for tests
            this.Configuration.FileSystem = fs ?? this.Configuration.FileSystem;

            this.Callbacks = this;
        }

        public bool OnSaving<TColor>(ImageBase<TColor> image, Stream stream, IImageEncoder encoder, IEncoderOptions options) where TColor : struct, IPixel<TColor>
        {
            this.Saves.Add(new OperationDetails
            {
                encoder = encoder,
                options = options,
                stream = stream
            });

            return false;
        }

        public bool OnProcessing<TColor>(ImageBase<TColor> image, IImageProcessor<TColor> processor, Rectangle rectangle) where TColor : struct, IPixel<TColor>
        {
            return false;
        }

        public struct OperationDetails
        {
            public Stream stream;
            public IImageEncoder encoder;
            public IEncoderOptions options;
        }
    }
}
