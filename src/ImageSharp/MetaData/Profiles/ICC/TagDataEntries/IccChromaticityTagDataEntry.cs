// <copyright file="IccChromaticityTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// The chromaticity tag type provides basic chromaticity data
    /// and type of phosphors or colorants of a monitor to applications and utilities.
    /// </summary>
    internal sealed class IccChromaticityTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccChromaticityTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantType">Colorant Type</param>
        public IccChromaticityTagDataEntry(IccColorantEncoding colorantType)
            : this(colorantType, GetColorantArray(colorantType), IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccChromaticityTagDataEntry"/> class.
        /// </summary>
        /// <param name="channelValues">Values per channel</param>
        public IccChromaticityTagDataEntry(double[][] channelValues)
            : this(IccColorantEncoding.Unknown, channelValues, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccChromaticityTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantType">Colorant Type</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccChromaticityTagDataEntry(IccColorantEncoding colorantType, IccProfileTag tagSignature)
            : this(colorantType, GetColorantArray(colorantType), tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccChromaticityTagDataEntry"/> class.
        /// </summary>
        /// <param name="channelValues">Values per channel</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccChromaticityTagDataEntry(double[][] channelValues, IccProfileTag tagSignature)
            : this(IccColorantEncoding.Unknown, channelValues, tagSignature)
        {
        }

        private IccChromaticityTagDataEntry(IccColorantEncoding colorantType, double[][] channelValues, IccProfileTag tagSignature)
            : base(IccTypeSignature.Chromaticity, tagSignature)
        {
            Guard.NotNull(channelValues, nameof(channelValues));
            Guard.MustBeBetweenOrEqualTo(channelValues.Length, 1, 15, nameof(channelValues));

            this.ColorantType = colorantType;
            this.ChannelValues = channelValues;

            int channelLength = channelValues[0].Length;
            bool channelsNotSame = channelValues.Any(t => t == null || t.Length != channelLength);
            Guard.IsFalse(channelsNotSame, nameof(channelValues), "The number of values per channel is not the same for all channels");
        }

        /// <summary>
        /// Gets the number of channels
        /// </summary>
        public int ChannelCount
        {
            get { return this.ChannelValues.Length; }
        }

        /// <summary>
        /// Gets the colorant type
        /// </summary>
        public IccColorantEncoding ColorantType { get; }

        /// <summary>
        /// Gets the values per channel
        /// </summary>
        public double[][] ChannelValues { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccChromaticityTagDataEntry entry)
            {
                return this.ColorantType == entry.ColorantType
                    && this.ChannelValues.SequenceEqual(entry.ChannelValues);
            }

            return false;
        }

        private static double[][] GetColorantArray(IccColorantEncoding colorantType)
        {
            switch (colorantType)
            {
                case IccColorantEncoding.EBU_Tech_3213_E:
                    return new double[][]
                    {
                        new double[] { 0.640, 0.330 },
                        new double[] { 0.290, 0.600 },
                        new double[] { 0.150, 0.060 },
                    };
                case IccColorantEncoding.ITU_R_BT_709_2:
                    return new double[][]
                    {
                        new double[] { 0.640, 0.330 },
                        new double[] { 0.300, 0.600 },
                        new double[] { 0.150, 0.060 },
                    };
                case IccColorantEncoding.P22:
                    return new double[][]
                    {
                        new double[] { 0.625, 0.340 },
                        new double[] { 0.280, 0.605 },
                        new double[] { 0.155, 0.070 },
                    };
                case IccColorantEncoding.SMPTE_RP145:
                    return new double[][]
                    {
                        new double[] { 0.630, 0.340 },
                        new double[] { 0.310, 0.595 },
                        new double[] { 0.155, 0.070 },
                    };
                default:
                    throw new ArgumentException("Unrecognized colorant encoding");
            }
        }
    }
}
