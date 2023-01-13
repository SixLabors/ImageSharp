// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp;

/// <summary>
/// A wrapper for nullable values that correctly handles the return type based on the result.
/// </summary>
/// <typeparam name="T">The type of nullable value.</typeparam>
public readonly struct Attempt<T>
{
    /// <summary>
    /// Gets a value indicating whether the attempted return was successful.
    /// </summary>
    [MemberNotNullWhen(returnValue: true, member: nameof(Value))]
    public bool Success => this.Value is not null;

    /// <summary>
    /// Gets the value when the attempted return is successful; otherwise, the default value for the type.
    /// </summary>
    public T? Value { get; init; }
}
