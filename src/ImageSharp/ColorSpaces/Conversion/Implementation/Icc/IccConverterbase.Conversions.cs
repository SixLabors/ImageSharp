// <copyright file="IccConverter.Conversions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Icc
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        /// <summary>
        /// A delegate for converting colors with an ICC profile
        /// </summary>
        /// <param name="values">The values to convert</param>
        /// <returns>The converted values</returns>
        protected delegate float[] ConversionDelegate(float[] values);

        /// <summary>
        /// Checks the profile for available conversion methods and gathers all the informations necessary for it
        /// </summary>
        /// <param name="profile">The profile to use for the conversion</param>
        /// <param name="toPcs">True if the conversion is to the Profile Connection Space</param>
        /// <param name="renderingIntent">The wanted rendering intent. Can be ignored if not available</param>
        /// <returns>A delegate that does the appropriate conversion</returns>
        protected ConversionDelegate Init(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent)
        {
            ConversionMethod method = this.GetConversionMethod(profile, renderingIntent);
            switch (method)
            {
                case ConversionMethod.D0:
                    return toPcs ? this.InitD(profile, IccProfileTag.DToB0) :
                        this.InitD(profile, IccProfileTag.BToD0);

                case ConversionMethod.D1:
                    return toPcs ? this.InitD(profile, IccProfileTag.DToB1) :
                        this.InitD(profile, IccProfileTag.BToD1);

                case ConversionMethod.D2:
                    return toPcs ? this.InitD(profile, IccProfileTag.DToB2) :
                        this.InitD(profile, IccProfileTag.BToD2);

                case ConversionMethod.D3:
                    return toPcs ? this.InitD(profile, IccProfileTag.DToB3) :
                        this.InitD(profile, IccProfileTag.BToD3);

                case ConversionMethod.A0:
                    return toPcs ? this.InitA(profile, IccProfileTag.AToB0) :
                        this.InitA(profile, IccProfileTag.BToA0);

                case ConversionMethod.A1:
                    return toPcs ? this.InitA(profile, IccProfileTag.AToB1) :
                        this.InitA(profile, IccProfileTag.BToA1);

                case ConversionMethod.A2:
                    return toPcs ? this.InitA(profile, IccProfileTag.AToB2) :
                        this.InitA(profile, IccProfileTag.BToA2);

                case ConversionMethod.ColorTrc:
                    return this.InitColorTrc(profile, toPcs);

                case ConversionMethod.GrayTrc:
                    return this.InitGrayTrc(profile, !toPcs);

                case ConversionMethod.Invalid:
                default:
                    throw new InvalidIccProfileException();
            }
        }

        private ConversionDelegate InitA(IccProfile profile, IccProfileTag tag)
        {
            IccTagDataEntry entry = this.GetTag(profile, tag);
            switch (entry)
            {
                case IccLut8TagDataEntry lut8:
                    return (values) => this.CalculateLut(lut8, values);
                case IccLut16TagDataEntry lut16:
                    return (values) => this.CalculateLut(lut16, values);
                case IccLutAToBTagDataEntry lutAtoB:
                    return (values) => this.CalculateLutAToB(lutAtoB, values);
                case IccLutBToATagDataEntry lutBtoA:
                    return (values) => this.CalculateLutBToA(lutBtoA, values);

                default:
                    throw new InvalidIccProfileException();
            }
        }

        private ConversionDelegate InitD(IccProfile profile, IccProfileTag tag)
        {
            IccMultiProcessElementsTagDataEntry entry = this.GetTag<IccMultiProcessElementsTagDataEntry>(profile, tag);
            if (entry == null)
            {
                throw new InvalidIccProfileException();
            }

            return (values) =>
            {
                float[] result = new float[values.Length];
                Array.Copy(values, result, values.Length);
                for (int i = 0; i < entry.Data.Length; i++)
                {
                    switch (entry.Data[i])
                    {
                        case IccCurveSetProcessElement curve:
                            result = this.CalculateMpeCurveSet(curve, result);
                            break;

                        case IccMatrixProcessElement matrix:
                            result = this.CalculateMpeMatrix(matrix, result);
                            break;

                        case IccClutProcessElement clut:
                            result = this.CalculateMpeClut(clut, result);
                            break;

                        default:
                            throw new InvalidIccProfileException();
                    }
                }

                return result;
            };
        }

        private ConversionDelegate InitColorTrc(IccProfile profile, bool toPcs)
        {
            IccXyzTagDataEntry redMatrixColumn = this.GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.RedMatrixColumn);
            IccXyzTagDataEntry greenMatrixColumn = this.GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.GreenMatrixColumn);
            IccXyzTagDataEntry blueMatrixColumn = this.GetTag<IccXyzTagDataEntry>(profile, IccProfileTag.BlueMatrixColumn);

            IccTagDataEntry redTrc = this.GetTag(profile, IccProfileTag.RedTrc);
            IccTagDataEntry greenTrc = this.GetTag(profile, IccProfileTag.GreenTrc);
            IccTagDataEntry blueTrc = this.GetTag(profile, IccProfileTag.BlueTrc);

            if (redMatrixColumn == null ||
                greenMatrixColumn == null ||
                blueMatrixColumn == null ||
                redTrc == null ||
                greenTrc == null ||
                blueTrc == null)
            {
                throw new InvalidIccProfileException();
            }

            Vector3 mr = redMatrixColumn.Data[0];
            Vector3 mg = greenMatrixColumn.Data[0];
            Vector3 mb = blueMatrixColumn.Data[0];
            var matrix = new Matrix4x4(mr.X, mr.Y, mr.Z, 0, mg.X, mg.Y, mg.Z, 0, mb.X, mb.Y, mb.Z, 0, 0, 0, 0, 1);

            if (toPcs)
            {
                return (values) =>
                {
                    var vector = new Vector3(
                        this.CalculateCurve(redTrc, false, values[0]),
                        this.CalculateCurve(greenTrc, false, values[1]),
                        this.CalculateCurve(blueTrc, false, values[2]));

                    var result = Vector3.Transform(vector, matrix);
                    return new float[3]
                    {
                        result.X,
                        result.Y,
                        result.Z,
                    };
                };
            }
            else
            {
                Matrix4x4.Invert(matrix, out matrix);

                return (values) =>
                {
                    var result = Vector3.Transform(new Vector3(values[0], values[1], values[2]), matrix);
                    return new float[3]
                    {
                        this.CalculateCurve(redTrc, true, result.X),
                        this.CalculateCurve(greenTrc, true, result.Y),
                        this.CalculateCurve(blueTrc, true, result.Z),
                    };
                };
            }
        }

        private ConversionDelegate InitGrayTrc(IccProfile profile, bool inverted)
        {
            IccTagDataEntry entry = this.GetTag(profile, IccProfileTag.GrayTrc);
            return (values) => new float[] { this.CalculateCurve(entry, inverted, values[0]) };
        }
    }
}
