// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The container for <see cref="TiffIfdEntry"/> array of IFD.
    /// </summary>
    internal class TiffIfdEntriesContainer
    {
        public TiffIfdEntriesContainer(TiffIfdEntry[] entries)
        {
            this.Entries = entries;
        }

        public TiffIfdEntry[] Entries { get; }

        public uint Width => this.GetSingleUIntValue(TiffTagId.ImageWidth);

        public uint Height => this.GetSingleUIntValue(TiffTagId.ImageLength);

        public TiffCompression Compression => this.GetSingleEnumValue<TiffCompression>(TiffTagId.Compression);

        public uint RowsPerStrip => this.GetSingleUIntValue(TiffTagId.RowsPerStrip);

        public uint[] StripOffsets => this.GetArrayValue<uint>(TiffTagId.StripOffsets);

        public uint[] StripByteCounts => this.GetArrayValue<uint>(TiffTagId.StripByteCounts);

        public TiffResolutionUnit ResolutionUnit => this.GetSingleEnumValue<TiffResolutionUnit>(TiffTagId.ResolutionUnit, TiffIfdEntryDefinitions.DefaultResolutionUnit);

        public TiffPlanarConfiguration PlanarConfiguration => this.GetSingleEnumValue<TiffPlanarConfiguration>(TiffTagId.PlanarConfiguration, TiffIfdEntryDefinitions.DefaultPlanarConfiguration);

        public TK GetSingleEnumValue<TK>(TiffTagId tag, TK? defaultValue = null)
            where TK : struct
        {
            if (!this.TryGetSingleValue(tag, out uint value))
            {
                if (defaultValue != null)
                {
                    return defaultValue.Value;
                }

                throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
            }

            return (TK)((object)value);
        }

        public uint GetSingleUIntValue(TiffTagId tag) => this.GetSingleValue<uint>(tag);

        public T GetSingleValue<T>(TiffTagId tag)
            where T : struct
        {
            if (this.TryGetSingleValue(tag, out T result))
            {
                return result;
            }

            throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
        }

        public bool TryGetSingleValue<T>(TiffTagId tag, out T result)
            where T : struct
        {
            for (int i = 0; i < this.Entries.Length; i++)
            {
                if (this.Entries[i].TagId == tag)
                {
                    var value = (T[])this.Entries[i].Value;
                    DebugGuard.IsTrue(this.Entries[i].Count == 1 && value.Length == 1, "Expected single value");

                    result = value[0];
                    return true;
                }
            }

            result = default;
            return false;
        }

        public bool TryGetSingleElementValue<T>(TiffTagId tag, out T result)
            where T : class
        {
            for (int i = 0; i < this.Entries.Length; i++)
            {
                if (this.Entries[i].TagId == tag)
                {
                    result = (T)this.Entries[i].Value;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public T[] GetArrayValue<T>(TiffTagId tag)
            where T : struct
        {
            if (this.TryGetArrayValue(tag, out T[] result))
            {
                return result;
            }

            throw new ArgumentException("Required tag is not founded: " + tag, nameof(tag));
        }

        public bool TryGetArrayValue<T>(TiffTagId tag, out T[] result)
            where T : struct
        {
            for (int i = 0; i < this.Entries.Length; i++)
            {
                if (this.Entries[i].TagId == tag)
                {
                    result = (T[])this.Entries[i].Value;
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Gets the child <see cref="TiffIfdEntry"/> with the specified tag ID.
        /// </summary>
        /// <param name="tag">The tag ID to search for.</param>
        /// <returns>The resulting <see cref="TiffIfdEntry"/>, or null if it does not exists.</returns>
        public TiffIfdEntry GetEntry(TiffTagId tag)
        {
            for (int i = 0; i < this.Entries.Length; i++)
            {
                if (this.Entries[i].TagId == tag)
                {
                    return this.Entries[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the child <see cref="TiffIfdEntry"/> with the specified tag ID.
        /// </summary>
        /// <param name="tag">The tag ID to search for.</param>
        /// <param name="entry">The resulting <see cref="TiffIfdEntry"/>, if it exists.</param>
        /// <returns>A flag indicating whether the requested entry exists.</returns>
        public bool TryGetEntry(TiffTagId tag, out TiffIfdEntry entry)
        {
            entry = this.GetEntry(tag);
            return entry != null;
        }
    }
}
