// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// A test image file.
/// </summary>
public class TestFormat : IImageFormatConfigurationModule, IImageFormat
{
    private readonly Dictionary<Type, object> sampleImages = new();

    // We should not change Configuration.Default in individual tests!
    // Create new configuration instances with new Configuration(TestFormat.GlobalTestFormat) instead!
    public static TestFormat GlobalTestFormat { get; } = new();

    public TestFormat()
    {
        this.Encoder = new TestEncoder(this);
        this.Decoder = new TestDecoder(this);
    }

    public List<DecodeOperation> DecodeCalls { get; } = new();

    public TestEncoder Encoder { get; }

    public TestDecoder Decoder { get; }

    private readonly byte[] header = Guid.NewGuid().ToByteArray();

    public MemoryStream CreateStream(byte[] marker = null)
    {
        var ms = new MemoryStream();
        byte[] data = this.header;
        ms.Write(data, 0, data.Length);
        if (marker != null)
        {
            ms.Write(marker, 0, marker.Length);
        }

        ms.Position = 0;
        return ms;
    }

    public Stream CreateAsyncSemaphoreStream(SemaphoreSlim notifyWaitPositionReachedSemaphore, SemaphoreSlim continueSemaphore, bool seeakable, int size = 1024, int waitAfterPosition = 512)
    {
        byte[] buffer = new byte[size];
        this.header.CopyTo(buffer, 0);
        var semaphoreStream = new SemaphoreReadMemoryStream(buffer, waitAfterPosition, notifyWaitPositionReachedSemaphore, continueSemaphore);
        return seeakable ? semaphoreStream : new AsyncStreamWrapper(semaphoreStream, () => false);
    }

    public void VerifySpecificDecodeCall<TPixel>(byte[] marker, Configuration config)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecodeOperation[] discovered = this.DecodeCalls.Where(x => x.IsMatch(marker, config, typeof(TPixel))).ToArray();

        Assert.True(discovered.Length > 0, "No calls to decode on this format with the provided options happened");

        foreach (DecodeOperation d in discovered)
        {
            this.DecodeCalls.Remove(d);
        }
    }

    public void VerifyAgnosticDecodeCall(byte[] marker, Configuration config)
    {
        DecodeOperation[] discovered = this.DecodeCalls.Where(x => x.IsMatch(marker, config, typeof(TestPixelForAgnosticDecode))).ToArray();

        Assert.True(discovered.Length > 0, "No calls to decode on this format with the provided options happened");

        foreach (DecodeOperation d in discovered)
        {
            this.DecodeCalls.Remove(d);
        }
    }

    public Image<TPixel> Sample<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        lock (this.sampleImages)
        {
            if (!this.sampleImages.ContainsKey(typeof(TPixel)))
            {
                this.sampleImages.Add(typeof(TPixel), new Image<TPixel>(1, 1));
            }

            return (Image<TPixel>)this.sampleImages[typeof(TPixel)];
        }
    }

    public Image SampleAgnostic() => this.Sample<TestPixelForAgnosticDecode>();

    public string MimeType => "img/test";

    public string Extension => "test_ext";

    public IEnumerable<string> SupportedExtensions => new[] { "test_ext" };

    public int HeaderSize => this.header.Length;

    public string Name => this.Extension;

    public string DefaultMimeType => this.MimeType;

    public IEnumerable<string> MimeTypes => new[] { this.MimeType };

    public IEnumerable<string> FileExtensions => this.SupportedExtensions;

    public bool IsSupportedFileFormat(ReadOnlySpan<byte> fileHeader)
    {
        if (fileHeader.Length < this.header.Length)
        {
            return false;
        }

        for (int i = 0; i < this.header.Length; i++)
        {
            if (fileHeader[i] != this.header[i])
            {
                return false;
            }
        }

        return true;
    }

    public void Configure(Configuration configuration)
    {
        configuration.ImageFormatsManager.AddImageFormatDetector(new TestHeader(this));
        configuration.ImageFormatsManager.SetEncoder(this, new TestEncoder(this));
        configuration.ImageFormatsManager.SetDecoder(this, new TestDecoder(this));
    }

    public struct DecodeOperation
    {
        public byte[] Marker;
        internal Configuration Config;

        public Type PixelType;

        public bool IsMatch(byte[] testMarker, Configuration config, Type pixelType)
        {
            if (this.Config != config || this.PixelType != pixelType)
            {
                return false;
            }

            if (testMarker.Length != this.Marker.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Marker.Length; i++)
            {
                if (testMarker[i] != this.Marker[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    public class TestHeader : IImageFormatDetector
    {
        private readonly TestFormat testFormat;

        public int HeaderSize => this.testFormat.HeaderSize;

        public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
        {
            format = this.testFormat.IsSupportedFileFormat(header) ? this.testFormat : null;

            return format != null;
        }

        public TestHeader(TestFormat testFormat) => this.testFormat = testFormat;
    }

    public class TestDecoder : SpecializedImageDecoder<TestDecoderOptions>
    {
        private readonly TestFormat testFormat;

        public TestDecoder(TestFormat testFormat) => this.testFormat = testFormat;

        public IEnumerable<string> MimeTypes => new[] { this.testFormat.MimeType };

        public IEnumerable<string> FileExtensions => this.testFormat.SupportedExtensions;

        public int HeaderSize => this.testFormat.HeaderSize;

        public bool IsSupportedFileFormat(Span<byte> header) => this.testFormat.IsSupportedFileFormat(header);

        protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Image<TestPixelForAgnosticDecode> image =
                this.Decode<TestPixelForAgnosticDecode>(this.CreateDefaultSpecializedOptions(options), stream, cancellationToken);
            ImageFrameCollection<TestPixelForAgnosticDecode> m = image.Frames;

            return new(image.PixelType, image.Size, image.Metadata, new List<ImageFrameMetadata>(image.Frames.Select(x => x.Metadata)));
        }

        protected override TestDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
            => new() { GeneralOptions = options };

        protected override Image<TPixel> Decode<TPixel>(TestDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Configuration configuration = options.GeneralOptions.Configuration;
            using MemoryStream ms = new();
            stream.CopyTo(ms, configuration.StreamProcessingBufferSize);
            byte[] marker = ms.ToArray().Skip(this.testFormat.header.Length).ToArray();
            this.testFormat.DecodeCalls.Add(new DecodeOperation
            {
                Marker = marker,
                Config = configuration,
                PixelType = typeof(TPixel)
            });

            // TODO record this happened so we can verify it.
            return this.testFormat.Sample<TPixel>();
        }

        protected override Image Decode(TestDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.Decode<TestPixelForAgnosticDecode>(options, stream, cancellationToken);
    }

    public class TestDecoderOptions : ISpecializedDecoderOptions
    {
        public DecoderOptions GeneralOptions { get; init; } = DecoderOptions.Default;
    }

    public class TestEncoder : IImageEncoder
    {
        private readonly TestFormat testFormat;

        public TestEncoder(TestFormat testFormat) => this.testFormat = testFormat;

        public IEnumerable<string> MimeTypes => new[] { this.testFormat.MimeType };

        public IEnumerable<string> FileExtensions => this.testFormat.SupportedExtensions;

        public bool SkipMetadata { get; init; }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // TODO record this happened so we can verify it.
        }

        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => Task.CompletedTask;  // TODO record this happened so we can verify it.
    }

    public struct TestPixelForAgnosticDecode : IPixel<TestPixelForAgnosticDecode>
    {
        public static PixelTypeInfo GetPixelTypeInfo()
            => PixelTypeInfo.Create<TestPixelForAgnosticDecode>(
                PixelComponentInfo.Create<TestPixelForAgnosticDecode>(2, 8, 8),
                PixelColorType.Red | PixelColorType.Green,
                PixelAlphaRepresentation.None);

        public PixelOperations<TestPixelForAgnosticDecode> CreatePixelOperations() => new();

        public void FromScaledVector4(Vector4 vector)
        {
        }

        public Vector4 ToScaledVector4() => default;

        public void FromVector4(Vector4 vector)
        {
        }

        public Vector4 ToVector4() => default;

        public void FromArgb32(Argb32 source)
        {
        }

        public void FromBgra5551(Bgra5551 source)
        {
        }

        public void FromBgr24(Bgr24 source)
        {
        }

        public void FromBgra32(Bgra32 source)
        {
        }

        public void FromAbgr32(Abgr32 source)
        {
        }

        public void FromL8(L8 source)
        {
        }

        public void FromL16(L16 source)
        {
        }

        public void FromLa16(La16 source)
        {
        }

        public void FromLa32(La32 source)
        {
        }

        public void FromRgb24(Rgb24 source)
        {
        }

        public void FromRgba32(Rgba32 source)
        {
        }

        public void ToRgba32(ref Rgba32 dest)
        {
        }

        public void FromRgb48(Rgb48 source)
        {
        }

        public void FromRgba64(Rgba64 source)
        {
        }

        public bool Equals(TestPixelForAgnosticDecode other) => false;
    }
}
