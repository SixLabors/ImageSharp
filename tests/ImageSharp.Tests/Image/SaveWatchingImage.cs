
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
    public class SaveWatchingImage : Image<Color>
    {
        public List<OperationDetails> Saves { get; } = new List<OperationDetails>();
        
        public SaveWatchingImage(int width, int height, IFileSystem fs = null)
           : base(width, height, Configuration.CreateDefaultInstance())
        {
            //switch out the file system for tests
            this.Configuration.FileSystem = fs ?? this.Configuration.FileSystem;
        }

        internal override void SaveInternal(Stream stream, IImageEncoder encoder, IEncoderOptions options)
        {

        }

        public struct OperationDetails
        {
            public Stream stream;
            public IImageEncoder encoder;
            public IEncoderOptions options;
        }
    }
}
