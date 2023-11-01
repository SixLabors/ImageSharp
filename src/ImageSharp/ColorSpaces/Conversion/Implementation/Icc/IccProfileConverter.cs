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
        Configuration configuration = image.Configuration;

        image.ProcessPixelRows(accessor =>
        {
            Vector3 illuminant = outputIccProfile.Header.PcsIlluminant;
            ColorSpaceConverter converter = new(new ColorSpaceConverterOptions()
            {
                WhitePoint = new(illuminant),
            });

            using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(accessor.Width);
            Span<Vector4> vectorsSpan = vectors.GetSpan();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<TPixel> row = accessor.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToVector4(configuration, row, vectorsSpan, PixelConversionModifiers.Scale);

                if (inputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieLab
                && outputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieXyz)
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        pcs = PcsToLab(pcs);
                        CieXyz xyz = converter.ToCieXyz(new CieLab(pcs.X, pcs.Y, pcs.Z, new CieXyz(inputIccProfile.Header.PcsIlluminant)));
                        pcs = new Vector4(xyz.X, xyz.Y, xyz.Z, pcs.W);

                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }
                else if (inputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieXyz
                && outputIccProfile.Header.ProfileConnectionSpace == IccColorSpaceType.CieLab)
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        CieLab lab = converter.ToCieLab(new CieXyz(pcs.X, pcs.Y, pcs.Z));
                        pcs = LabToPcs(pcs, lab);
                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }
                else
                {
                    for (int x = 0; x < vectorsSpan.Length; x++)
                    {
                        Vector4 pcs = converterDataToPcs.Calculate(vectorsSpan[x]);
                        vectorsSpan[x] = converterPcsToData.Calculate(pcs);
                    }
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, vectorsSpan, row, PixelConversionModifiers.Scale);
            }
        });

        image.Metadata.IccProfile = outputIccProfile;
    }

    private static unsafe Vector4 PcsToLab(Vector4 input)
    {
        Vector3* v = (Vector3*)&input;
        v[0] *= 100F;
        v[0] -= new Vector3(0, 128F, 128F);
        return input;
    }

    private static unsafe Vector4 LabToPcs(Vector4 input, CieLab lab)
    {
        Vector3* v = (Vector3*)&input;
        v[0] = new Vector3(lab.L, lab.A + 128F, lab.B + 128F);
        v[0] /= 100F;
        return input;
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
