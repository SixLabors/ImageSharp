// <copyright file="IccMultiProcessElementsTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// This structure represents a color transform, containing
    /// a sequence of processing elements.
    /// </summary>
    internal sealed class IccMultiProcessElementsTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiProcessElementsTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Processing elements</param>
        public IccMultiProcessElementsTagDataEntry(IccMultiProcessElement[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiProcessElementsTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Processing elements</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccMultiProcessElementsTagDataEntry(IccMultiProcessElement[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.MultiProcessElements, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            Guard.IsTrue(data.Length < 1, nameof(data), $"{nameof(data)} must have at least one element");

            this.InputChannelCount = data[0].InputChannelCount;
            this.OutputChannelCount = data[0].OutputChannelCount;
            this.Data = data;

            bool channelsNotSame = data.Any(t => t.InputChannelCount != this.InputChannelCount || t.OutputChannelCount != this.OutputChannelCount);
            Guard.IsTrue(channelsNotSame, nameof(data), "The number of input and output channels are not the same for all elements");
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount { get; }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount { get; }

        /// <summary>
        /// Gets the processing elements
        /// </summary>
        public IccMultiProcessElement[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccMultiProcessElementsTagDataEntry entry)
            {
                return this.InputChannelCount == entry.InputChannelCount
                    && this.OutputChannelCount == entry.OutputChannelCount
                    && this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
