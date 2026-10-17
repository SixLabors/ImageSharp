// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using static SixLabors.ImageSharp.ColorProfiles.Icc.IccColorConverter;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal abstract partial class IccConverterBase
{
    private IIccColorConverter calculator;

    /// <summary>
    /// Gets a value indicating whether PCS values use a sixteen-bit LUT's legacy encoding.
    /// </summary>
    internal bool Is16BitLutEntry => this.calculator.Is16BitLut;

    /// <summary>
    /// Gets a value indicating whether the selected transform uses channel curves and a matrix.
    /// </summary>
    internal bool IsTrc => this.calculator.IsTrc;

    /// <summary>
    /// Selects a closed connection once, before row traversal. Each SIMD block then uses direct operator calls.
    /// </summary>
    /// <param name="target">The target profile converter.</param>
    /// <param name="pcs">The connection's PCS encoding and adjustment rules.</param>
    /// <returns>The converter for the complete profile connection.</returns>
    internal IIccColorConverter Connect(IccConverterBase target, PcsOperator pcs)
        => this.calculator switch
        {
            IccColorConverter<ColorTrcOperator> source when pcs.CanCombineXyzMatrices
                && target.calculator is IccColorConverter<ColorTrcOperator> destination =>
                source.Operator.Connect(destination.Operator, pcs),
            IccColorConverter<ColorTrcOperator> source => Connect(source.Operator, target.calculator, pcs),
            IccColorConverter<GrayTrcOperator> source => Connect(source.Operator, target.calculator, pcs),
            IccColorConverter<LutColorOperator> source => Connect(source.Operator, target.calculator, pcs),
            IccColorConverter<SampledLutColorOperator> source => Connect(source.Operator, target.calculator, pcs),
            _ => throw new InvalidIccProfileException("Unsupported ICC source operator.")
        };

    /// <summary>
    /// Pairs the selected source operator with the target operator selected by this factory.
    /// </summary>
    /// <typeparam name="TSource">The closed source operator type.</typeparam>
    /// <param name="source">The configured source transform.</param>
    /// <param name="target">The target profile converter.</param>
    /// <param name="pcs">The connection's PCS encoding and adjustment rules.</param>
    /// <returns>The closed converter for both profile operators.</returns>
    private static IIccColorConverter Connect<TSource>(TSource source, IIccColorConverter target, PcsOperator pcs)
        where TSource : struct, IIccColorConverterOperator
        => target switch
        {
            IccColorConverter<ColorTrcOperator> value =>
                value.Operator.ConnectFrom(source, pcs),
            IccColorConverter<GrayTrcOperator> value =>
                new IccColorConverter<ConnectionOperator<TSource, GrayTrcOperator>>(new(source, pcs, value.Operator)),
            IccColorConverter<LutColorOperator> value =>
                new IccColorConverter<ConnectionOperator<TSource, LutColorOperator>>(new(source, pcs, value.Operator)),
            IccColorConverter<SampledLutColorOperator> value =>
                new IccColorConverter<ConnectionOperator<TSource, SampledLutColorOperator>>(new(source, pcs, value.Operator)),
            _ => throw new InvalidIccProfileException("Unsupported ICC target operator.")
        };

    /// <summary>
    /// Checks the profile for available conversion methods and gathers all the information's necessary for it.
    /// </summary>
    /// <param name="profile">The profile to use for the conversion.</param>
    /// <param name="toPcs">True if the conversion is to the Profile Connection Space.</param>
    /// <param name="renderingIntent">The wanted rendering intent. Can be ignored if not available.</param>
    /// <exception cref="InvalidIccProfileException">Invalid conversion method.</exception>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    protected void Init(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent, IccInterpolationMethod interpolationMethod)
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
                                InitA(profile, IccProfileTag.AToB0, interpolationMethod) :
                                InitA(profile, IccProfileTag.BToA0, interpolationMethod),
            ConversionMethod.A1 => toPcs ?
                                InitA(profile, IccProfileTag.AToB1, interpolationMethod) :
                                InitA(profile, IccProfileTag.BToA1, interpolationMethod),
            ConversionMethod.A2 => toPcs ?
                                InitA(profile, IccProfileTag.AToB2, interpolationMethod) :
                                InitA(profile, IccProfileTag.BToA2, interpolationMethod),
            ConversionMethod.ColorTrc => InitColorTrc(profile, toPcs),
            ConversionMethod.GrayTrc => InitGrayTrc(profile, toPcs),
            _ => throw new InvalidIccProfileException("Invalid conversion method."),
        };

    /// <summary>
    /// Creates a LUT calculator with interpolation selected for its input color space and direction.
    /// </summary>
    /// <param name="profile">The profile containing the table.</param>
    /// <param name="tag">The transform tag to evaluate.</param>
    /// <returns>The configured table calculator.</returns>
    /// <param name="interpolationMethod">The requested interpolation method.</param>
    private static IIccColorConverter InitA(IccProfile profile, IccProfileTag tag, IccInterpolationMethod interpolationMethod)
    {
        // Lab-indexed output and linking tables use independent-axis interpolation.
        // Device-to-PCS tables use tetrahedra in their final three input dimensions.
        bool useTrilinearInterpolation = interpolationMethod == IccInterpolationMethod.Trilinear
            || (interpolationMethod == IccInterpolationMethod.Auto
                && profile.Header.ProfileConnectionSpace == IccColorSpaceType.CieLab
                && (tag is IccProfileTag.BToA0 or IccProfileTag.BToA1 or IccProfileTag.BToA2
                    || profile.Header.Class is IccProfileClass.DeviceLink or IccProfileClass.Abstract));

        return GetTag(profile, tag) switch
        {
            IccLut8TagDataEntry lut8 => new IccColorConverter<SampledLutColorOperator>(new(lut8, useTrilinearInterpolation)),
            IccLut16TagDataEntry lut16 => new IccColorConverter<SampledLutColorOperator>(new(lut16, useTrilinearInterpolation)),
            IccLutAToBTagDataEntry lutAtoB => new IccColorConverter<LutColorOperator>(new LutColorOperator(lutAtoB, useTrilinearInterpolation)),
            IccLutBToATagDataEntry lutBtoA => new IccColorConverter<LutColorOperator>(new LutColorOperator(lutBtoA, useTrilinearInterpolation)),
            _ => throw new InvalidIccProfileException($"Invalid entry {tag}."),
        };
    }

    private static IIccColorConverter InitD(IccProfile profile, IccProfileTag tag)
    {
        IccMultiProcessElementsTagDataEntry entry = GetTag<IccMultiProcessElementsTagDataEntry>(profile, tag)
            ?? throw new InvalidIccProfileException("Entry is null.");

        throw new NotImplementedException("Multi process elements are not supported");
    }

    private static IccColorConverter<ColorTrcOperator> InitColorTrc(IccProfile profile, bool toPcs)
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

        ColorTrcOperator colorOperator = new(
            redMatrixColumn.Data[0],
            greenMatrixColumn.Data[0],
            blueMatrixColumn.Data[0],
            [redTrc, greenTrc, blueTrc],
            toPcs);

        return new IccColorConverter<ColorTrcOperator>(colorOperator);
    }

    private static IccColorConverter<GrayTrcOperator> InitGrayTrc(IccProfile profile, bool toPcs)
    {
        IccTagDataEntry entry = GetTag(profile, IccProfileTag.GrayTrc);
        return new IccColorConverter<GrayTrcOperator>(new GrayTrcOperator(entry, profile.Header.ProfileConnectionSpace, toPcs));
    }
}
