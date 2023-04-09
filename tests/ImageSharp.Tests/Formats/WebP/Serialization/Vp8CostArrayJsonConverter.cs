// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp.Formats.Webp.Lossy;

namespace SixLabors.ImageSharp.Tests.Formats.WebP.Serialization;

internal class Vp8CostArrayJsonConverter : JsonConverter<Vp8CostArray>
{
    public override Vp8CostArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, Vp8CostArray value, JsonSerializerOptions options) => throw new NotImplementedException();
}
