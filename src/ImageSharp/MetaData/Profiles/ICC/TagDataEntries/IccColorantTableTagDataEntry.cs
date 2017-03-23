// <copyright file="IccColorantTableTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// The purpose of this tag is to identify the colorants used in
    /// the profile by a unique name and set of PCSXYZ or PCSLAB values
    /// to give the colorant an unambiguous value.
    /// </summary>
    internal sealed class IccColorantTableTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantTableTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantData">Colorant Data</param>
        public IccColorantTableTagDataEntry(IccColorantTableEntry[] colorantData)
            : this(colorantData, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantTableTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantData">Colorant Data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccColorantTableTagDataEntry(IccColorantTableEntry[] colorantData, IccProfileTag tagSignature)
            : base(IccTypeSignature.ColorantTable, tagSignature)
        {
            Guard.NotNull(colorantData, nameof(colorantData));
            Guard.MustBeBetweenOrEqualTo(colorantData.Length, 1, 15, nameof(colorantData));

            this.ColorantData = colorantData;
        }

        /// <summary>
        /// Gets the colorant data
        /// </summary>
        public IccColorantTableEntry[] ColorantData { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccColorantTableTagDataEntry entry)
            {
                return this.ColorantData.SequenceEqual(entry.ColorantData);
            }

            return false;
        }
    }
}
