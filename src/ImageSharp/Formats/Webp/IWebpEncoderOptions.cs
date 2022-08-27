// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Configuration options for use during webp encoding.
    /// </summary>
    internal interface IWebpEncoderOptions
    {
        /// <summary>
        /// Gets the webp file format used. Either lossless or lossy.
        /// Defaults to lossy.
        /// </summary>
        WebpFileFormatType? FileFormat { get; }

        /// <summary>
        /// Gets the compression quality. Between 0 and 100.
        /// For lossy, 0 gives the smallest size and 100 the largest. For lossless,
        /// this parameter is the amount of effort put into the compression: 0 is the fastest but gives larger
        /// files compared to the slowest, but best, 100.
        /// Defaults to 75.
        /// </summary>
        int Quality { get; }

        /// <summary>
        /// Gets the encoding method to use. Its a quality/speed trade-off (0=fast, 6=slower-better).
        /// Defaults to 4.
        /// </summary>
        WebpEncodingMethod Method { get; }

        /// <summary>
        /// Gets a value indicating whether the alpha plane should be compressed with Webp lossless format.
        /// Defaults to true.
        /// </summary>
        bool UseAlphaCompression { get; }

        /// <summary>
        /// Gets the number of entropy-analysis passes (in [1..10]).
        /// Defaults to 1.
        /// </summary>
        int EntropyPasses { get; }

        /// <summary>
        /// Gets the amplitude of the spatial noise shaping. Spatial noise shaping (or sns for short) refers to a general collection of built-in algorithms
        /// used to decide which area of the picture should use relatively less bits, and where else to better transfer these bits.
        /// The possible range goes from 0 (algorithm is off) to 100 (the maximal effect).
        /// Defaults to 50.
        /// </summary>
        int SpatialNoiseShaping { get; }

        /// <summary>
        /// Gets the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering).
        /// A value of 0 will turn off any filtering. Higher value will increase the strength of the filtering process applied after decoding the picture.
        /// The higher the value the smoother the picture will appear.
        /// Typical values are usually in the range of 20 to 50.
        /// Defaults to 60.
        /// </summary>
        int FilterStrength { get; }

        /// <summary>
        /// Gets a value indicating whether to preserve the exact RGB values under transparent area. Otherwise, discard this invisible
        /// RGB information for better compression.
        /// The default value is Clear.
        /// </summary>
        WebpTransparentColorMode TransparentColorMode { get; }

        /// <summary>
        /// Gets a value indicating whether near lossless mode should be used.
        /// This option adjusts pixel values to help compressibility, but has minimal impact on the visual quality.
        /// </summary>
        bool NearLossless { get; }

        /// <summary>
        /// Gets the quality of near-lossless image preprocessing. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).
        /// The typical value is around 60. Note that lossy with -q 100 can at times yield better results.
        /// </summary>
        int NearLosslessQuality { get; }
    }
}
