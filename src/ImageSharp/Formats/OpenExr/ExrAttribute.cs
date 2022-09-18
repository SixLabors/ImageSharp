// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License..

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.OpenExr;

[DebuggerDisplay("Name: {Name}, Type: {Type}, Length: {Length}")]
internal class ExrAttribute
{
    public static readonly ExrAttribute EmptyAttribute = new(string.Empty, string.Empty, 0);

    public ExrAttribute(string name, string type, int length)
    {
        this.Name = name;
        this.Type = type;
        this.Length = length;
    }

    public string Name { get; }

    public string Type { get; }

    public int Length { get; }
}

