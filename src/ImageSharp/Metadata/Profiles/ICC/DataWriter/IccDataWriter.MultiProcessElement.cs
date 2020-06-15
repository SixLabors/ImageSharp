// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Provides methods to write ICC data types
    /// </summary>
    internal sealed partial class IccDataWriter
    {
        /// <summary>
        /// Writes a <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMultiProcessElement(IccMultiProcessElement value)
        {
            int count = this.WriteUInt32((uint)value.Signature);
            count += this.WriteUInt16((ushort)value.InputChannelCount);
            count += this.WriteUInt16((ushort)value.OutputChannelCount);

            switch (value.Signature)
            {
                case IccMultiProcessElementSignature.CurveSet:
                    return count + this.WriteCurveSetProcessElement((IccCurveSetProcessElement)value);
                case IccMultiProcessElementSignature.Matrix:
                    return count + this.WriteMatrixProcessElement((IccMatrixProcessElement)value);
                case IccMultiProcessElementSignature.Clut:
                    return count + this.WriteClutProcessElement((IccClutProcessElement)value);

                case IccMultiProcessElementSignature.BAcs:
                case IccMultiProcessElementSignature.EAcs:
                    return count + this.WriteEmpty(8);

                default:
                    throw new InvalidIccProfileException($"Invalid MultiProcessElement type of {value.Signature}");
            }
        }

        /// <summary>
        /// Writes a CurveSet <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCurveSetProcessElement(IccCurveSetProcessElement value)
        {
            int count = 0;
            foreach (IccOneDimensionalCurve curve in value.Curves)
            {
                count += this.WriteOneDimensionalCurve(curve);
                count += this.WritePadding();
            }

            return count;
        }

        /// <summary>
        /// Writes a Matrix <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrixProcessElement(IccMatrixProcessElement value)
        {
            return this.WriteMatrix(value.MatrixIxO, true)
                 + this.WriteMatrix(value.MatrixOx1, true);
        }

        /// <summary>
        /// Writes a CLUT <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteClutProcessElement(IccClutProcessElement value)
        {
            return this.WriteClut(value.ClutValue);
        }
    }
}
