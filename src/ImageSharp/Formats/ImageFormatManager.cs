// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Collection of Image Formats to be used in <see cref="Configuration" /> class.
/// </summary>
public class ImageFormatManager
{
    /// <summary>
    /// Used for locking against as there is no ConcurrentSet type.
    /// <see href="https://github.com/dotnet/corefx/issues/6318"/>
    /// </summary>
    private static readonly object HashLock = new();

    /// <summary>
    /// The list of supported <see cref="IImageEncoder"/> keyed to mime types.
    /// </summary>
    private readonly ConcurrentDictionary<IImageFormat, IImageEncoder> mimeTypeEncoders = new();

    /// <summary>
    /// The list of supported <see cref="IImageDecoder"/> keyed to mime types.
    /// </summary>
    private readonly ConcurrentDictionary<IImageFormat, IImageDecoder> mimeTypeDecoders = new();

    /// <summary>
    /// The list of supported <see cref="IImageFormat"/>s.
    /// </summary>
    private readonly HashSet<IImageFormat> imageFormats = new();

    /// <summary>
    /// The list of supported <see cref="IImageFormatDetector"/>s.
    /// </summary>
    private ConcurrentBag<IImageFormatDetector> imageFormatDetectors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFormatManager" /> class.
    /// </summary>
    public ImageFormatManager()
    {
    }

    /// <summary>
    /// Gets the maximum header size of all the formats.
    /// </summary>
    internal int MaxHeaderSize { get; private set; }

    /// <summary>
    /// Gets the currently registered <see cref="IImageFormat"/>s.
    /// </summary>
    public IEnumerable<IImageFormat> ImageFormats => this.imageFormats;

    /// <summary>
    /// Gets the currently registered <see cref="IImageFormatDetector"/>s.
    /// </summary>
    internal IEnumerable<IImageFormatDetector> FormatDetectors => this.imageFormatDetectors;

    /// <summary>
    /// Gets the currently registered <see cref="IImageDecoder"/>s.
    /// </summary>
    internal IEnumerable<KeyValuePair<IImageFormat, IImageDecoder>> ImageDecoders => this.mimeTypeDecoders;

    /// <summary>
    /// Gets the currently registered <see cref="IImageEncoder"/>s.
    /// </summary>
    internal IEnumerable<KeyValuePair<IImageFormat, IImageEncoder>> ImageEncoders => this.mimeTypeEncoders;

    /// <summary>
    /// Registers a new format provider.
    /// </summary>
    /// <param name="format">The format to register as a known format.</param>
    public void AddImageFormat(IImageFormat format)
    {
        Guard.NotNull(format, nameof(format));
        Guard.NotNull(format.MimeTypes, nameof(format.MimeTypes));
        Guard.NotNull(format.FileExtensions, nameof(format.FileExtensions));

        lock (HashLock)
        {
            this.imageFormats.Add(format);
        }
    }

    /// <summary>
    /// For the specified file extensions type find the e <see cref="IImageFormat"/>.
    /// </summary>
    /// <param name="extension">The extension to return the format for.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given extension;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    public bool TryFindFormatByFileExtension(string extension, [NotNullWhen(true)] out IImageFormat? format)
    {
        if (!string.IsNullOrWhiteSpace(extension) && extension[0] == '.')
        {
            extension = extension[1..];
        }

        format = this.imageFormats.FirstOrDefault(x =>
            x.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));

        return format is not null;
    }

    /// <summary>
    /// For the specified mime type find the <see cref="IImageFormat"/>.
    /// </summary>
    /// <param name="mimeType">The mime-type to return the format for.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given mime-type;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    public bool TryFindFormatByMimeType(string mimeType, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.imageFormats.FirstOrDefault(x => x.MimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase));
        return format is not null;
    }

    internal bool TryFindFormatByDecoder(IImageDecoder decoder, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.mimeTypeDecoders.FirstOrDefault(x => x.Value.GetType() == decoder.GetType()).Key;
        return format is not null;
    }

    /// <summary>
    /// Sets a specific image encoder as the encoder for a specific image format.
    /// </summary>
    /// <param name="imageFormat">The image format to register the encoder for.</param>
    /// <param name="encoder">The encoder to use,</param>
    public void SetEncoder(IImageFormat imageFormat, IImageEncoder encoder)
    {
        Guard.NotNull(imageFormat, nameof(imageFormat));
        Guard.NotNull(encoder, nameof(encoder));
        this.AddImageFormat(imageFormat);
        this.mimeTypeEncoders.AddOrUpdate(imageFormat, encoder, (_, _) => encoder);
    }

    /// <summary>
    /// Sets a specific image decoder as the decoder for a specific image format.
    /// </summary>
    /// <param name="imageFormat">The image format to register the encoder for.</param>
    /// <param name="decoder">The decoder to use,</param>
    public void SetDecoder(IImageFormat imageFormat, IImageDecoder decoder)
    {
        Guard.NotNull(imageFormat, nameof(imageFormat));
        Guard.NotNull(decoder, nameof(decoder));
        this.AddImageFormat(imageFormat);
        this.mimeTypeDecoders.AddOrUpdate(imageFormat, decoder, (_, _) => decoder);
    }

    /// <summary>
    /// Removes all the registered image format detectors.
    /// </summary>
    public void ClearImageFormatDetectors() => this.imageFormatDetectors = new ConcurrentBag<IImageFormatDetector>();

    /// <summary>
    /// Adds a new detector for detecting mime types.
    /// </summary>
    /// <param name="detector">The detector to add</param>
    public void AddImageFormatDetector(IImageFormatDetector detector)
    {
        Guard.NotNull(detector, nameof(detector));
        this.imageFormatDetectors.Add(detector);
        this.SetMaxHeaderSize();
    }

    /// <summary>
    /// For the specified mime type find the decoder.
    /// </summary>
    /// <param name="format">The format to discover</param>
    /// <returns>The <see cref="IImageDecoder"/>.</returns>
    /// <exception cref="UnknownImageFormatException">The format is not registered.</exception>
    public IImageDecoder GetDecoder(IImageFormat format)
    {
        Guard.NotNull(format, nameof(format));

        if (!this.mimeTypeDecoders.TryGetValue(format, out IImageDecoder? decoder))
        {
            ThrowInvalidDecoder(this);
        }

        return decoder;
    }

    /// <summary>
    /// For the specified mime type find the encoder.
    /// </summary>
    /// <param name="format">The format to discover</param>
    /// <returns>The <see cref="IImageEncoder"/>.</returns>
    /// <exception cref="UnknownImageFormatException">The format is not registered.</exception>
    public IImageEncoder GetEncoder(IImageFormat format)
    {
        Guard.NotNull(format, nameof(format));

        if (!this.mimeTypeEncoders.TryGetValue(format, out IImageEncoder? encoder))
        {
            ThrowInvalidDecoder(this);
        }

        return encoder;
    }

    /// <summary>
    /// Sets the max header size.
    /// </summary>
    private void SetMaxHeaderSize() => this.MaxHeaderSize = this.imageFormatDetectors.Max(x => x.HeaderSize);

    [DoesNotReturn]
    internal static void ThrowInvalidDecoder(ImageFormatManager manager)
    {
        StringBuilder sb = new();
        sb = sb.AppendLine("Image cannot be loaded. Available decoders:");

        foreach (KeyValuePair<IImageFormat, IImageDecoder> val in manager.ImageDecoders)
        {
            sb = sb.AppendFormat(CultureInfo.InvariantCulture, " - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
        }

        throw new UnknownImageFormatException(sb.ToString());
    }
}
