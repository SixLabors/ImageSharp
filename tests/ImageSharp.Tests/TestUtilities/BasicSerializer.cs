// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.ComponentModel;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

/// <summary>
/// RemoteExecutor can only execute static methods, which can only consume string arguments,
/// because data is being passed on command line interface. This utility allows serialization
/// of <see cref="IXunitSerializable"/> types to strings.
/// </summary>
internal class BasicSerializer : IXunitSerializationInfo
{
    private readonly Dictionary<string, string> map = [];

    public const char Separator = ':';

    private string DumpToString(Type type)
    {
        using MemoryStream ms = new();
        using StreamWriter writer = new(ms);
        writer.WriteLine(type.FullName);
        foreach (KeyValuePair<string, string> kv in this.map)
        {
            writer.WriteLine($"{kv.Key}{Separator}{kv.Value}");
        }

        writer.Flush();
        byte[] data = ms.ToArray();
        return Convert.ToBase64String(data);
    }

    private Type LoadDump(string dump)
    {
        byte[] data = Convert.FromBase64String(dump);

        using MemoryStream ms = new(data);
        using StreamReader reader = new(ms);
        Type type = Type.GetType(reader.ReadLine());
        for (string s = reader.ReadLine(); s != null; s = reader.ReadLine())
        {
            string[] kv = s.Split(Separator);
            this.map[kv[0]] = kv[1];
        }

        return type;
    }

    public static string Serialize(IXunitSerializable serializable)
    {
        BasicSerializer serializer = new();
        serializable.Serialize(serializer);
        return serializer.DumpToString(serializable.GetType());
    }

    public static T Deserialize<T>(string dump)
        where T : IXunitSerializable
    {
        BasicSerializer serializer = new();
        Type type = serializer.LoadDump(dump);

        T result = (T)Activator.CreateInstance(type);
        result.Deserialize(serializer);
        return result;
    }

    public void AddValue(string key, object value, Type type = null)
    {
        Guard.NotNull(key, nameof(key));
        if (value == null)
        {
            return;
        }

        type ??= value.GetType();

        this.map[key] = TypeDescriptor.GetConverter(type).ConvertToInvariantString(value);
    }

    public object GetValue(string key, Type type)
    {
        Guard.NotNull(key, nameof(key));

        if (!this.map.TryGetValue(key, out string str))
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(str);
    }

    public T GetValue<T>(string key) => (T)this.GetValue(key, typeof(T));
}
