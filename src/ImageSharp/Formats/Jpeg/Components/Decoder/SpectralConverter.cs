// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Converter used to convert jpeg spectral data to pixels.
/// </summary>
internal abstract class SpectralConverter
{
    /// <summary>
    /// Supported scaled spectral block sizes for scaled IDCT decoding.
    /// </summary>
    private static readonly int[] ScaledBlockSizes =
    [

        // 8 => 1, 1/8 of the original size
        1,

        // 8 => 2, 1/4 of the original size
        2,

        // 8 => 4, 1/2 of the original size
        4,
    ];

    /// <summary>
    /// Gets a value indicating whether this converter has converted spectral
    /// data of the current image or not.
    /// </summary>
    protected bool Converted { get; private set; }

    /// <summary>
    /// Injects jpeg image decoding metadata.
    /// </summary>
    /// <remarks>
    /// This should be called exactly once during SOF (Start Of Frame) marker.
    /// </remarks>
    /// <param name="frame"><see cref="JpegFrame"/>Instance containing decoder-specific parameters.</param>
    /// <param name="jpegData"><see cref="IRawJpegData"/>Instance containing decoder-specific parameters.</param>
    public abstract void InjectFrameData(JpegFrame frame, IRawJpegData jpegData);

    /// <summary>
    /// Initializes this spectral decoder instance for decoding.
    /// This should be called exactly once after all markers which can alter
    /// spectral decoding parameters.
    /// </summary>
    public abstract void PrepareForDecoding();

    /// <summary>
    /// Converts single spectral jpeg stride to color stride in baseline
    /// decoding mode.
    /// </summary>
    /// <param name="iccProfile">
    /// The ICC profile to use for color conversion. If <see langword="null"/>, then the default color space is used.
    /// </param>
    /// <remarks>
    /// Called once per decoded spectral stride in <see cref="HuffmanScanDecoder"/>
    /// only for baseline interleaved jpeg images.
    /// Spectral 'stride' doesn't particularly mean 'single stride'.
    /// Actual stride height depends on the subsampling factor of the given image.
    /// </remarks>
    public abstract void ConvertStrideBaseline(IccProfile? iccProfile);

    /// <summary>
    /// Marks current converter state as 'converted'.
    /// </summary>
    /// <remarks>
    /// This must be called only for baseline interleaved jpeg's.
    /// </remarks>
    public void CommitConversion()
    {
        DebugGuard.IsFalse(this.Converted, nameof(this.Converted), $"{nameof(this.CommitConversion)} must be called only once");

        this.Converted = true;
    }

    /// <summary>
    /// Gets the color converter.
    /// </summary>
    /// <param name="frame">The jpeg frame with the color space to convert to.</param>
    /// <param name="jpegData">The raw JPEG data.</param>
    /// <returns>The color converter.</returns>
    protected virtual JpegColorConverterBase GetColorConverter(JpegFrame frame, IRawJpegData jpegData)
        => JpegColorConverterBase.GetConverter(jpegData.ColorSpace, frame.Precision);

    /// <summary>
    /// Calculates image size with optional scaling.
    /// </summary>
    /// <remarks>
    /// Does not apply scaling if <paramref name="targetSize"/> is null.
    /// </remarks>
    /// <param name="size">Size of the image.</param>
    /// <param name="targetSize">Target size of the image.</param>
    /// <param name="blockPixelSize">Spectral block size, equals to 8 if scaling is not applied.</param>
    /// <returns>Resulting image size, equals to <paramref name="size"/> if scaling is not applied.</returns>
    public static Size CalculateResultingImageSize(Size size, Size? targetSize, out int blockPixelSize)
    {
        const int blockNativePixelSize = 8;

        blockPixelSize = blockNativePixelSize;
        if (targetSize != null)
        {
            Size tSize = targetSize.Value;

            int fullBlocksWidth = (int)((uint)size.Width / blockNativePixelSize);
            int fullBlocksHeight = (int)((uint)size.Height / blockNativePixelSize);

            // & (blockNativePixelSize - 1) is Numerics.Modulo8(), basically
            int blockWidthRemainder = size.Width & (blockNativePixelSize - 1);
            int blockHeightRemainder = size.Height & (blockNativePixelSize - 1);

            for (int i = 0; i < ScaledBlockSizes.Length; i++)
            {
                int blockSize = ScaledBlockSizes[i];
                int scaledWidth = (fullBlocksWidth * blockSize) + (int)Numerics.DivideCeil((uint)(blockWidthRemainder * blockSize), blockNativePixelSize);
                int scaledHeight = (fullBlocksHeight * blockSize) + (int)Numerics.DivideCeil((uint)(blockHeightRemainder * blockSize), blockNativePixelSize);

                if (scaledWidth >= tSize.Width && scaledHeight >= tSize.Height)
                {
                    blockPixelSize = blockSize;
                    return new(scaledWidth, scaledHeight);
                }
            }
        }

        return size;
    }

    /// <summary>
    /// Gets a value indicating whether the converter has a pixel buffer.
    /// </summary>
    /// <returns><see langword="true"/> if the converter has a pixel buffer; otherwise, <see langword="false"/>.</returns>
    public abstract bool HasPixelBuffer();
}
