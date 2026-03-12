// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Defines the contract for all color profile connection spaces.
/// </summary>
public interface IProfileConnectingSpace;

/// <summary>
/// Defines the contract for all color profile connection spaces.
/// </summary>
/// <typeparam name="TSelf">The type of color profile.</typeparam>
/// <typeparam name="TProfileSpace">The type of color profile connecting space.</typeparam>
public interface IProfileConnectingSpace<TSelf, TProfileSpace> : IColorProfile<TSelf, TProfileSpace>, IProfileConnectingSpace
    where TSelf : struct, IColorProfile<TSelf, TProfileSpace>, IProfileConnectingSpace
    where TProfileSpace : struct, IProfileConnectingSpace;
