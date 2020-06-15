// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Contains methods for writing ICC profiles.
    /// </summary>
    internal sealed class IccWriter
    {
        /// <summary>
        /// Writes the ICC profile into a byte array
        /// </summary>
        /// <param name="profile">The ICC profile to write</param>
        /// <returns>The ICC profile as a byte array</returns>
        public byte[] Write(IccProfile profile)
        {
            Guard.NotNull(profile, nameof(profile));

            using (var writer = new IccDataWriter())
            {
                IccTagTableEntry[] tagTable = this.WriteTagData(writer, profile.Entries);
                this.WriteTagTable(writer, tagTable);
                this.WriteHeader(writer, profile.Header);
                return writer.GetData();
            }
        }

        private void WriteHeader(IccDataWriter writer, IccProfileHeader header)
        {
            writer.SetIndex(0);

            writer.WriteUInt32(writer.Length);
            writer.WriteAsciiString(header.CmmType, 4, false);
            writer.WriteVersionNumber(header.Version);
            writer.WriteUInt32((uint)header.Class);
            writer.WriteUInt32((uint)header.DataColorSpace);
            writer.WriteUInt32((uint)header.ProfileConnectionSpace);
            writer.WriteDateTime(header.CreationDate);
            writer.WriteAsciiString("acsp");
            writer.WriteUInt32((uint)header.PrimaryPlatformSignature);
            writer.WriteInt32((int)header.Flags);
            writer.WriteUInt32(header.DeviceManufacturer);
            writer.WriteUInt32(header.DeviceModel);
            writer.WriteInt64((long)header.DeviceAttributes);
            writer.WriteUInt32((uint)header.RenderingIntent);
            writer.WriteXyzNumber(header.PcsIlluminant);
            writer.WriteAsciiString(header.CreatorSignature, 4, false);

            IccProfileId id = IccProfile.CalculateHash(writer.GetData());
            writer.WriteProfileId(id);
        }

        private void WriteTagTable(IccDataWriter writer, IccTagTableEntry[] table)
        {
            // 128 = size of ICC header
            writer.SetIndex(128);

            writer.WriteUInt32((uint)table.Length);
            foreach (IccTagTableEntry entry in table)
            {
                writer.WriteUInt32((uint)entry.Signature);
                writer.WriteUInt32(entry.Offset);
                writer.WriteUInt32(entry.DataSize);
            }
        }

        private IccTagTableEntry[] WriteTagData(IccDataWriter writer, IccTagDataEntry[] entries)
        {
            // TODO: Investigate cost of Linq GroupBy
            IEnumerable<IGrouping<IccTagDataEntry, IccTagDataEntry>> grouped = entries.GroupBy(t => t);

            // (Header size) + (entry count) + (nr of entries) * (size of table entry)
            writer.SetIndex(128 + 4 + (entries.Length * 12));

            var table = new List<IccTagTableEntry>();
            foreach (IGrouping<IccTagDataEntry, IccTagDataEntry> group in grouped)
            {
                writer.WriteTagDataEntry(group.Key, out IccTagTableEntry tableEntry);
                foreach (IccTagDataEntry item in group)
                {
                    table.Add(new IccTagTableEntry(item.TagSignature, tableEntry.Offset, tableEntry.DataSize));
                }
            }

            return table.ToArray();
        }
    }
}