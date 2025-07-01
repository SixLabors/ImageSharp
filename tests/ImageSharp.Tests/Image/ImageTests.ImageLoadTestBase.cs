// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public abstract class ImageLoadTestBase : IDisposable
    {
        private Lazy<Stream> dataStreamLazy;

        protected Image<Rgba32> localStreamReturnImageRgba32;

        protected Image<Bgra4444> localStreamReturnImageAgnostic;

        protected Mock<IImageDecoder> localDecoder;

        protected IImageFormatDetector localMimeTypeDetector;

        protected Mock<IImageFormat> localImageFormatMock;

        protected readonly string MockFilePath = Guid.NewGuid().ToString();

        internal readonly Mock<IFileSystem> LocalFileSystemMock = new();

        protected readonly TestFileSystem topLevelFileSystem = new();

        public Configuration LocalConfiguration { get; }

        public TestFormat TestFormat { get; } = new();

        /// <summary>
        /// Gets the top-level configuration in the context of this test case.
        /// It has <see cref="TestFormat"/> registered.
        /// </summary>
        public Configuration TopLevelConfiguration { get; }

        public byte[] Marker { get; }

        public Stream DataStream => this.dataStreamLazy.Value;

        public byte[] DecodedData { get; private set; }

        protected byte[] ByteArray => ((MemoryStream)this.DataStream).ToArray();

        protected ImageInfo LocalImageInfo { get; }

        protected ImageLoadTestBase()
        {
            // TODO: Remove all this mocking. It's too complicated and we can now use fakes.
            this.localStreamReturnImageRgba32 = new Image<Rgba32>(1, 1);
            this.localStreamReturnImageAgnostic = new Image<Bgra4444>(1, 1);
            this.LocalImageInfo = new ImageInfo(new Size(1, 1), new ImageMetadata { DecodedImageFormat = PngFormat.Instance });

            this.localImageFormatMock = new Mock<IImageFormat>();

            this.localDecoder = new Mock<IImageDecoder>();
            this.localDecoder.Setup(x => x.Identify(It.IsAny<DecoderOptions>(), It.IsAny<Stream>()))
                .Returns(this.LocalImageInfo);

            this.localDecoder.Setup(x => x.IdentifyAsync(It.IsAny<DecoderOptions>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(this.LocalImageInfo));

            this.localDecoder
                .Setup(x => x.Decode<Rgba32>(It.IsAny<DecoderOptions>(), It.IsAny<Stream>()))
                .Callback<DecoderOptions, Stream>((_, s) =>
                    {
                        using MemoryStream ms = new();
                        s.CopyTo(ms);
                        this.DecodedData = ms.ToArray();
                    })
                .Returns(this.localStreamReturnImageRgba32);

            this.localDecoder
                .Setup(x => x.Decode(It.IsAny<DecoderOptions>(), It.IsAny<Stream>()))
                .Callback<DecoderOptions, Stream>((_, s) =>
                    {
                        using MemoryStream ms = new();
                        s.CopyTo(ms);
                        this.DecodedData = ms.ToArray();
                    })
                .Returns(this.localStreamReturnImageAgnostic);

            this.localDecoder
                .Setup(x => x.DecodeAsync<Rgba32>(It.IsAny<DecoderOptions>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<DecoderOptions, Stream, CancellationToken>((_, s, _) =>
                {
                    using MemoryStream ms = new();
                    s.CopyTo(ms);
                    this.DecodedData = ms.ToArray();
                })
                .Returns(Task.FromResult(this.localStreamReturnImageRgba32));

            this.localDecoder
                .Setup(x => x.DecodeAsync(It.IsAny<DecoderOptions>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<DecoderOptions, Stream, CancellationToken>((_, s, _) =>
                {
                    using MemoryStream ms = new();
                    s.CopyTo(ms);
                    this.DecodedData = ms.ToArray();
                })
                .Returns(Task.FromResult<Image>(this.localStreamReturnImageAgnostic));

            this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormatMock.Object);

            this.LocalConfiguration = new Configuration();
            this.LocalConfiguration.ImageFormatsManager.AddImageFormatDetector(this.localMimeTypeDetector);
            this.LocalConfiguration.ImageFormatsManager.SetDecoder(this.localImageFormatMock.Object, this.localDecoder.Object);

            this.TopLevelConfiguration = new Configuration(this.TestFormat);

            this.Marker = Guid.NewGuid().ToByteArray();

            this.dataStreamLazy = new Lazy<Stream>(this.CreateStream);
            Stream StreamFactory() => this.DataStream;

            this.LocalFileSystemMock.Setup(x => x.OpenRead(this.MockFilePath)).Returns(StreamFactory);
            this.LocalFileSystemMock.Setup(x => x.OpenReadAsynchronous(this.MockFilePath)).Returns(StreamFactory);
            this.topLevelFileSystem.AddFile(this.MockFilePath, StreamFactory);
            this.LocalConfiguration.FileSystem = this.LocalFileSystemMock.Object;
            this.TopLevelConfiguration.FileSystem = this.topLevelFileSystem;
        }

        public void Dispose()
        {
            // Clean up the global object;
            this.localStreamReturnImageRgba32?.Dispose();
            this.localStreamReturnImageAgnostic?.Dispose();

            if (this.dataStreamLazy.IsValueCreated)
            {
                this.dataStreamLazy.Value.Dispose();
            }
        }

        protected virtual Stream CreateStream() => this.TestFormat.CreateStream(this.Marker);
    }
}
