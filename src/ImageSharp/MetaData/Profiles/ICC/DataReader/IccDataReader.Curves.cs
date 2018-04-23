// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed partial class IccDataReader
    {
        /// <summary>
        /// Reads a <see cref="IccOneDimensionalCurve"/>
        /// </summary>
        /// <returns>The read curve</returns>
        public IccOneDimensionalCurve ReadOneDimensionalCurve()
        {
            ushort segmentCount = this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            float[] breakPoints = new float[segmentCount - 1];
            for (int i = 0; i < breakPoints.Length; i++)
            {
                breakPoints[i] = this.ReadSingle();
            }

            IccCurveSegment[] segments = new IccCurveSegment[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                segments[i] = this.ReadCurveSegment();
            }

            return new IccOneDimensionalCurve(breakPoints, segments);
        }

        /// <summary>
        /// Reads a <see cref="IccResponseCurve"/>
        /// </summary>
        /// <param name="channelCount">The number of channels</param>
        /// <returns>The read curve</returns>
        public IccResponseCurve ReadResponseCurve(int channelCount)
        {
            var type = (IccCurveMeasurementEncodings)this.ReadUInt32();
            uint[] measurment = new uint[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                measurment[i] = this.ReadUInt32();
            }

            Vector3[] xyzValues = new Vector3[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                xyzValues[i] = this.ReadXyzNumber();
            }

            IccResponseNumber[][] response = new IccResponseNumber[channelCount][];
            for (int i = 0; i < channelCount; i++)
            {
                response[i] = new IccResponseNumber[measurment[i]];
                for (uint j = 0; j < measurment[i]; j++)
                {
                    response[i][j] = this.ReadResponseNumber();
                }
            }

            return new IccResponseCurve(type, xyzValues, response);
        }

        /// <summary>
        /// Reads a <see cref="IccParametricCurve"/>
        /// </summary>
        /// <returns>The read curve</returns>
        public IccParametricCurve ReadParametricCurve()
        {
            ushort type = this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            float gamma, a, b, c, d, e, f;
            gamma = a = b = c = d = e = f = 0;

            if (type <= 4)
            {
                gamma = this.ReadFix16();
            }

            if (type > 0 && type <= 4)
            {
                a = this.ReadFix16();
                b = this.ReadFix16();
            }

            if (type > 1 && type <= 4)
            {
                c = this.ReadFix16();
            }

            if (type > 2 && type <= 4)
            {
                d = this.ReadFix16();
            }

            if (type == 4)
            {
                e = this.ReadFix16();
                f = this.ReadFix16();
            }

            switch (type)
            {
                case 0: return new IccParametricCurve(gamma);
                case 1: return new IccParametricCurve(gamma, a, b);
                case 2: return new IccParametricCurve(gamma, a, b, c);
                case 3: return new IccParametricCurve(gamma, a, b, c, d);
                case 4: return new IccParametricCurve(gamma, a, b, c, d, e, f);
                default: throw new InvalidIccProfileException($"Invalid parametric curve type of {type}");
            }
        }

        /// <summary>
        /// Reads a <see cref="IccCurveSegment"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccCurveSegment ReadCurveSegment()
        {
            var signature = (IccCurveSegmentSignature)this.ReadUInt32();
            this.AddIndex(4);   // 4 bytes reserved

            switch (signature)
            {
                case IccCurveSegmentSignature.FormulaCurve:
                    return this.ReadFormulaCurveElement();
                case IccCurveSegmentSignature.SampledCurve:
                    return this.ReadSampledCurveElement();
                default:
                    throw new InvalidIccProfileException($"Invalid curve segment type of {signature}");
            }
        }

        /// <summary>
        /// Reads a <see cref="IccFormulaCurveElement"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccFormulaCurveElement ReadFormulaCurveElement()
        {
            var type = (IccFormulaCurveType)this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            float gamma, a, b, c, d, e;
            gamma = d = e = 0;

            if (type == IccFormulaCurveType.Type1 || type == IccFormulaCurveType.Type2)
            {
                gamma = this.ReadSingle();
            }

            a = this.ReadSingle();
            b = this.ReadSingle();
            c = this.ReadSingle();

            if (type == IccFormulaCurveType.Type2 || type == IccFormulaCurveType.Type3)
            {
                d = this.ReadSingle();
            }

            if (type == IccFormulaCurveType.Type3)
            {
                e = this.ReadSingle();
            }

            return new IccFormulaCurveElement(type, gamma, a, b, c, d, e);
        }

        /// <summary>
        /// Reads a <see cref="IccSampledCurveElement"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccSampledCurveElement ReadSampledCurveElement()
        {
            uint count = this.ReadUInt32();
            float[] entries = new float[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = this.ReadSingle();
            }

            return new IccSampledCurveElement(entries);
        }

        /// <summary>
        /// Reads curve data
        /// </summary>
        /// <param name="count">Number of input channels</param>
        /// <returns>The curve data</returns>
        private IccTagDataEntry[] ReadCurves(int count)
        {
            var tdata = new IccTagDataEntry[count];
            for (int i = 0; i < count; i++)
            {
                IccTypeSignature type = this.ReadTagDataEntryHeader();
                if (type != IccTypeSignature.Curve && type != IccTypeSignature.ParametricCurve)
                {
                    throw new InvalidIccProfileException($"Curve has to be either \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.Curve)}\" or" +
                        $" \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.ParametricCurve)}\" for LutAToB- and LutBToA-TagDataEntries");
                }

                if (type == IccTypeSignature.Curve)
                {
                    tdata[i] = this.ReadCurveTagDataEntry();
                }
                else if (type == IccTypeSignature.ParametricCurve)
                {
                    tdata[i] = this.ReadParametricCurveTagDataEntry();
                }

                this.AddPadding();
            }

            return tdata;
        }
    }
}
