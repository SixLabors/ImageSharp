namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.IO;

    using Moq;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.IO;
    using SixLabors.ImageSharp.PixelFormats;

    public partial class ImageTests
    {
        public abstract class ImageLoadTestBase : IDisposable
        {
            

            protected Image<Rgba32> returnImage;

            protected Mock<IImageDecoder> localDecoder;

            

            protected IImageFormatDetector localMimeTypeDetector;

            protected Mock<IImageFormat> localImageFormatMock;

            public Configuration LocalConfiguration { get; }

            public TestFormat TestFormat { get; } = new TestFormat();

            /// <summary>
            /// Gets the top-level configuration in the context of this test case.
            /// It has <see cref="TestFormat"/> registered.
            /// </summary>
            public Configuration TopLevelConfiguration { get; }

            public byte[] Marker { get; private set; }

            public MemoryStream DataStream { get; private set; }

            public byte[] DecodedData { get; private set; }

            protected ImageLoadTestBase()
            {
                this.returnImage = new Image<Rgba32>(1, 1);

                this.localImageFormatMock = new Mock<IImageFormat>();

                this.localDecoder = new Mock<IImageDecoder>();
                this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormatMock.Object);
                this.localDecoder.Setup(x => x.Decode<Rgba32>(It.IsAny<Configuration>(), It.IsAny<Stream>()))

                    .Callback<Configuration, Stream>((c, s) =>
                        {
                            using (var ms = new MemoryStream())
                            {
                                s.CopyTo(ms);
                                this.DecodedData = ms.ToArray();
                            }
                        })
                    .Returns(this.returnImage);

                

                this.LocalConfiguration = new Configuration
                                              {
                                              };
                this.LocalConfiguration.ImageFormatsManager.AddImageFormatDetector(this.localMimeTypeDetector);
                this.LocalConfiguration.ImageFormatsManager.SetDecoder(this.localImageFormatMock.Object, this.localDecoder.Object);

                this.TopLevelConfiguration = new Configuration(this.TestFormat);

                this.Marker = Guid.NewGuid().ToByteArray();
                this.DataStream = this.TestFormat.CreateStream(this.Marker);

                
            }
            public void Dispose()
            {
                // clean up the global object;
                this.returnImage?.Dispose();
            }
        }
    }
}