// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc;

/// <summary>
/// Allows the conversion between ICC profiles.
/// </summary>
internal static class IccProfileConverter
{
    /// <summary>
    /// Performs a conversion of the image pixels based on the input and output ICC profiles.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <param name="inputIccProfile">The input ICC profile.</param>
    /// <param name="outputIccProfile">The output ICC profile. </param>
    public static void Convert(Image image, IccProfile inputIccProfile, IccProfile outputIccProfile)
        => image.AcceptVisitor(new IccProfileConverterVisitor(inputIccProfile, outputIccProfile));

    /// <summary>
    /// Performs a conversion of the image pixels based on the input and output ICC profiles.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel.</typeparam>
    /// <param name="image">The image to convert.</param>
    /// <param name="inputIccProfile">The input ICC profile.</param>
    /// <param name="outputIccProfile">The output ICC profile. </param>
    public static void Convert<TPixel>(Image<TPixel> image, IccProfile inputIccProfile, IccProfile outputIccProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IccDataToPcsConverter converterDataToPcs = new(inputIccProfile);
        IccPcsToDataConverter converterPcsToData = new(outputIccProfile);
        Configuration configuration = image.GetConfiguration();

        image.ProcessPixelRows(accessor =>
        {
            using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(accessor.Width);
            Span<Vector4> vectorsSpan = vectors.GetSpan();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<TPixel> row = accessor.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToVector4(configuration, row, vectorsSpan, PixelConversionModifiers.Scale);

                for (int x = 0; x < vectorsSpan.Length; x++)
                {
                    Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                    vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorsSpan, row);
            }
        });

        image.Metadata.IccProfile = outputIccProfile;
    }

    private readonly struct IccProfileConverterVisitor : IImageVisitor
    {
        private readonly IccProfile inputIccProfile;
        private readonly IccProfile outputIccProfile;

        public IccProfileConverterVisitor(IccProfile inputIccProfile, IccProfile outputIccProfile)
        {
            this.inputIccProfile = inputIccProfile;
            this.outputIccProfile = outputIccProfile;
        }

        public void Visit<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel> => Convert(image, this.inputIccProfile, this.outputIccProfile);
    }
}
