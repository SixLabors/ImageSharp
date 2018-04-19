// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// ICC Profile description
    /// </summary>
    internal readonly struct IccProfileDescription : IEquatable<IccProfileDescription>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileDescription"/> struct.
        /// </summary>
        /// <param name="deviceManufacturer">Device Manufacturer</param>
        /// <param name="deviceModel">Device Model</param>
        /// <param name="deviceAttributes">Device Attributes</param>
        /// <param name="technologyInformation">Technology Information</param>
        /// <param name="deviceManufacturerInfo">Device Manufacturer Info</param>
        /// <param name="deviceModelInfo">Device Model Info</param>
        public IccProfileDescription(
            uint deviceManufacturer,
            uint deviceModel,
            IccDeviceAttribute deviceAttributes,
            IccProfileTag technologyInformation,
            IccLocalizedString[] deviceManufacturerInfo,
            IccLocalizedString[] deviceModelInfo)
        {
            Guard.NotNull(deviceManufacturerInfo, nameof(deviceManufacturerInfo));
            Guard.NotNull(deviceModelInfo, nameof(deviceModelInfo));

            this.DeviceManufacturer = deviceManufacturer;
            this.DeviceModel = deviceModel;
            this.DeviceAttributes = deviceAttributes;
            this.TechnologyInformation = technologyInformation;
            this.DeviceManufacturerInfo = deviceManufacturerInfo;
            this.DeviceModelInfo = deviceModelInfo;
        }

        /// <summary>
        /// Gets the device manufacturer.
        /// </summary>
        public uint DeviceManufacturer { get; }

        /// <summary>
        /// Gets the device model.
        /// </summary>
        public uint DeviceModel { get; }

        /// <summary>
        /// Gets the device attributes.
        /// </summary>
        public IccDeviceAttribute DeviceAttributes { get; }

        /// <summary>
        /// Gets the technology information.
        /// </summary>
        public IccProfileTag TechnologyInformation { get; }

        /// <summary>
        /// Gets the device manufacturer info.
        /// </summary>
        public IccLocalizedString[] DeviceManufacturerInfo { get; }

        /// <summary>
        /// Gets the device model info.
        /// </summary>
        public IccLocalizedString[] DeviceModelInfo { get; }

        /// <inheritdoc/>
        public bool Equals(IccProfileDescription other) =>
            this.DeviceManufacturer == other.DeviceManufacturer &&
            this.DeviceModel == other.DeviceModel &&
            this.DeviceAttributes == other.DeviceAttributes &&
            this.TechnologyInformation == other.TechnologyInformation &&
            this.DeviceManufacturerInfo.SequenceEqual(other.DeviceManufacturerInfo) &&
            this.DeviceModelInfo.SequenceEqual(other.DeviceModelInfo);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is IccProfileDescription other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)this.DeviceManufacturer;
                hashCode = (hashCode * 397) ^ (int)this.DeviceModel;
                hashCode = (hashCode * 397) ^ this.DeviceAttributes.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.TechnologyInformation;
                hashCode = (hashCode * 397) ^ (this.DeviceManufacturerInfo?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.DeviceModelInfo?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
