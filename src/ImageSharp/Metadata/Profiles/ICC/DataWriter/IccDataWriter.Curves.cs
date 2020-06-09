// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <content>
    /// Provides methods to write ICC data types
    /// </content>
    internal sealed partial class IccDataWriter
    {
        /// <summary>
        /// Writes a <see cref="IccOneDimensionalCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteOneDimensionalCurve(IccOneDimensionalCurve value)
        {
            int count = this.WriteUInt16((ushort)value.Segments.Length);
            count += this.WriteEmpty(2);

            foreach (float point in value.BreakPoints)
            {
                count += this.WriteSingle(point);
            }

            foreach (IccCurveSegment segment in value.Segments)
            {
                count += this.WriteCurveSegment(segment);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccResponseCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteResponseCurve(IccResponseCurve value)
        {
            int count = this.WriteUInt32((uint)value.CurveType);

            foreach (IccResponseNumber[] responseArray in value.ResponseArrays)
            {
                count += this.WriteUInt32((uint)responseArray.Length);
            }

            foreach (Vector3 xyz in value.XyzValues)
            {
                count += this.WriteXyzNumber(xyz);
            }

            foreach (IccResponseNumber[] responseArray in value.ResponseArrays)
            {
                foreach (IccResponseNumber response in responseArray)
                {
                    count += this.WriteResponseNumber(response);
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccParametricCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteParametricCurve(IccParametricCurve value)
        {
            ushort typeValue = (ushort)value.Type;
            int count = this.WriteUInt16(typeValue);
            count += this.WriteEmpty(2);

            if (typeValue <= 4)
            {
                count += this.WriteFix16(value.G);
            }

            if (typeValue > 0 && typeValue <= 4)
            {
                count += this.WriteFix16(value.A);
                count += this.WriteFix16(value.B);
            }

            if (typeValue > 1 && typeValue <= 4)
            {
                count += this.WriteFix16(value.C);
            }

            if (typeValue > 2 && typeValue <= 4)
            {
                count += this.WriteFix16(value.D);
            }

            if (typeValue == 4)
            {
                count += this.WriteFix16(value.E);
                count += this.WriteFix16(value.F);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccCurveSegment"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCurveSegment(IccCurveSegment value)
        {
            int count = this.WriteUInt32((uint)value.Signature);
            count += this.WriteEmpty(4);

            switch (value.Signature)
            {
                case IccCurveSegmentSignature.FormulaCurve:
                    return count + this.WriteFormulaCurveElement((IccFormulaCurveElement)value);
                case IccCurveSegmentSignature.SampledCurve:
                    return count + this.WriteSampledCurveElement((IccSampledCurveElement)value);
                default:
                    throw new InvalidIccProfileException($"Invalid CurveSegment type of {value.Signature}");
            }
        }

        /// <summary>
        /// Writes a <see cref="IccFormulaCurveElement"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteFormulaCurveElement(IccFormulaCurveElement value)
        {
            int count = this.WriteUInt16((ushort)value.Type);
            count += this.WriteEmpty(2);

            if (value.Type == IccFormulaCurveType.Type1 || value.Type == IccFormulaCurveType.Type2)
            {
                count += this.WriteSingle(value.Gamma);
            }

            count += this.WriteSingle(value.A);
            count += this.WriteSingle(value.B);
            count += this.WriteSingle(value.C);

            if (value.Type == IccFormulaCurveType.Type2 || value.Type == IccFormulaCurveType.Type3)
            {
                count += this.WriteSingle(value.D);
            }

            if (value.Type == IccFormulaCurveType.Type3)
            {
                count += this.WriteSingle(value.E);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccSampledCurveElement"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteSampledCurveElement(IccSampledCurveElement value)
        {
            int count = this.WriteUInt32((uint)value.CurveEntries.Length);
            foreach (float entry in value.CurveEntries)
            {
                count += this.WriteSingle(entry);
            }

            return count;
        }
    }
}
