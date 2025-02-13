// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal abstract partial class IccConverterBase
{
    private IVector4Calculator calculator;

    internal bool Is16BitLutEntry => this.calculator is LutEntryCalculator { Is16Bit: true };

    internal bool IsTrc => this.calculator is ColorTrcCalculator or GrayTrcCalculator;

    /// <summary>
    /// Checks the profile for available conversion methods and gathers all the information's necessary for it.
    /// </summary>
    /// <param name="profile">The profile to use for the conversion.</param>
    /// <param name="toPcs">True if the conversion is to the Profile Connection Space.</param>
    /// <param name="renderingIntent">The wanted rendering intent. Can be ignored if not available.</param>
    /// <exception cref="InvalidIccProfileException">Invalid conversion method.</exception>
    protected void Init(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        => this.calculator = GetConversionMethod(profile, renderingIntent) switch
        {
            ConversionMethod.D0 => toPcs ?
                                InitD(profile, IccProfileTag.DToB0) :
                                InitD(profile, IccProfileTag.BToD0),
            ConversionMethod.D1 => toPcs ?
                                InitD(profile, IccProfileTag.DToB1) :
                                InitD(profile, IccProfileTag.BToD1),
            ConversionMethod.D2 => toPcs ?
                                InitD(profile, IccProfileTag.DToB2) :
                                InitD(profile, IccProfileTag.BToD2),
            ConversionMethod.D3 => toPcs ?
                                InitD(profile, IccProfileTag.DToB3) :
                                InitD(profile, IccProfileTag.BToD3),
            ConversionMethod.A0 => toPcs ?
                                InitA(profile, IccProfileTag.AToB0) :
                                InitA(profile, IccProfileTag.BToA0),
            ConversionMethod.A1 => toPcs ?
                                InitA(profile, IccProfileTag.AToB1) :
                                InitA(profile, IccProfileTag.BToA1),
            ConversionMethod.A2 => toPcs ?
                                InitA(profile, IccProfileTag.AToB2) :
                                InitA(profile, IccProfileTag.BToA2),
            ConversionMethod.ColorTrc => InitColorTrc(profile, toPcs),
            ConversionMethod.GrayTrc => InitGrayTrc(profile, toPcs),
            _ => throw new InvalidIccProfileException("Invalid conversion method."),
        };

    private static IVector4Calculator InitA(IccProfile profile, IccProfileTag tag)
        => GetTag(profile, tag) switch
        {
            IccLut8TagDataEntry lut8 => new LutEntryCalculator(lut8),
            IccLut16TagDataEntry lut16 => new LutEntryCalculator(lut16),
            IccLutAToBTagDataEntry lutAtoB => new LutABCalculator(lutAtoB),
            IccLutBToATagDataEntry lutBtoA => new LutABCalculator(lutBtoA),
            _ => throw new InvalidIccProfileException("Invalid entry."),
        };

    private static IVector4Calculator InitD(IccProfile profile, IccProfileTag tag)
    {
        IccMultiProcessElementsTagDataEntry entry = GetTag<IccMultiProcessElementsTagDataEntry>(profile, tag)
            ?? throw new InvalidIccProfileException("Entry is null.");

        throw new NotImplementedException("Multi process elements are not supported");
    }

    private static ColorTrcCalculator InitColorTrc(IccProfile profile, bool toPcs)
    {
        IccXyzTagDataEntry redMatrixColumn = GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.RedMatrixColumn);
        IccXyzTagDataEntry greenMatrixColumn = GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.GreenMatrixColumn);
        IccXyzTagDataEntry blueMatrixColumn = GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.BlueMatrixColumn);

        IccTagDataEntry redTrc = GetTag(profile, IccProfileTag.RedTrc);
        IccTagDataEntry greenTrc = GetTag(profile, IccProfileTag.GreenTrc);
        IccTagDataEntry blueTrc = GetTag(profile, IccProfileTag.BlueTrc);

        if (redMatrixColumn == null ||
            greenMatrixColumn == null ||
            blueMatrixColumn == null ||
            redTrc == null ||
            greenTrc == null ||
            blueTrc == null)
        {
            throw new InvalidIccProfileException("Missing matrix column or channel.");
        }

        return new ColorTrcCalculator(
            redMatrixColumn,
            greenMatrixColumn,
            blueMatrixColumn,
            redTrc,
            greenTrc,
            blueTrc,
            toPcs);
    }

    private static GrayTrcCalculator InitGrayTrc(IccProfile profile, bool toPcs)
    {
        IccTagDataEntry entry = GetTag(profile, IccProfileTag.GrayTrc);
        return new GrayTrcCalculator(entry, toPcs);
    }
}
