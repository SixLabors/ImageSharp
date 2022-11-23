// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        private IVector4Calculator calculator;

        /// <summary>
        /// Checks the profile for available conversion methods and gathers all the informations necessary for it
        /// </summary>
        /// <param name="profile">The profile to use for the conversion</param>
        /// <param name="toPcs">True if the conversion is to the Profile Connection Space</param>
        /// <param name="renderingIntent">The wanted rendering intent. Can be ignored if not available</param>
        protected void Init(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        {
            ConversionMethod method = GetConversionMethod(profile, renderingIntent);
            switch (method)
            {
                case ConversionMethod.D0:
                    this.calculator = toPcs ?
                        InitD(profile, IccProfileTag.DToB0) :
                        InitD(profile, IccProfileTag.BToD0);
                    break;

                case ConversionMethod.D1:
                    this.calculator = toPcs ?
                        InitD(profile, IccProfileTag.DToB1) :
                        InitD(profile, IccProfileTag.BToD1);
                    break;

                case ConversionMethod.D2:
                    this.calculator = toPcs ?
                        InitD(profile, IccProfileTag.DToB2) :
                        InitD(profile, IccProfileTag.BToD2);
                    break;

                case ConversionMethod.D3:
                    this.calculator = toPcs ?
                        InitD(profile, IccProfileTag.DToB3) :
                        InitD(profile, IccProfileTag.BToD3);
                    break;

                case ConversionMethod.A0:
                    this.calculator = toPcs ?
                        InitA(profile, IccProfileTag.AToB0) :
                        InitA(profile, IccProfileTag.BToA0);
                    break;

                case ConversionMethod.A1:
                    this.calculator = toPcs ?
                        InitA(profile, IccProfileTag.AToB1) :
                        InitA(profile, IccProfileTag.BToA1);
                    break;

                case ConversionMethod.A2:
                    this.calculator = toPcs ?
                        InitA(profile, IccProfileTag.AToB2) :
                        InitA(profile, IccProfileTag.BToA2);
                    break;

                case ConversionMethod.ColorTrc:
                    this.calculator = InitColorTrc(profile, toPcs);
                    break;

                case ConversionMethod.GrayTrc:
                    this.calculator = InitGrayTrc(profile, toPcs);
                    break;

                case ConversionMethod.Invalid:
                default:
                    throw new InvalidIccProfileException("Invalid conversion method.");
            }
        }

        private static IVector4Calculator InitA(IccProfile profile, IccProfileTag tag)
        {
            IccTagDataEntry entry = GetTag(profile, tag);
            switch (entry)
            {
                case IccLut8TagDataEntry lut8:
                    return new LutEntryCalculator(lut8);
                case IccLut16TagDataEntry lut16:
                    return new LutEntryCalculator(lut16);
                case IccLutAToBTagDataEntry lutAtoB:
                    return new LutABCalculator(lutAtoB);
                case IccLutBToATagDataEntry lutBtoA:
                    return new LutABCalculator(lutBtoA);

                default:
                    throw new InvalidIccProfileException("Invalid entry.");
            }
        }

        private static IVector4Calculator InitD(IccProfile profile, IccProfileTag tag)
        {
            IccMultiProcessElementsTagDataEntry entry = GetTag<IccMultiProcessElementsTagDataEntry>(profile, tag);
            if (entry == null)
            {
                throw new InvalidIccProfileException("Entry is null.");
            }

            throw new NotImplementedException("Multi process elements are not supported");
        }

        private static IVector4Calculator InitColorTrc(IccProfile profile, bool toPcs)
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

        private static IVector4Calculator InitGrayTrc(IccProfile profile, bool toPcs)
        {
            IccTagDataEntry entry = GetTag(profile, IccProfileTag.GrayTrc);
            return new GrayTrcCalculator(entry, toPcs);
        }
    }
}
