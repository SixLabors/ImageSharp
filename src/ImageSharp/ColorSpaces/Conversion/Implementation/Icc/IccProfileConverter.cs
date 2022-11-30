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
internal static class IccProfileConverter
{
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
}
