// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.ComponentModel;
using Xunit.Sdk;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

/// <summary>
/// RemoteExecutor can only execute static methods, which can only consume string arguments,
/// because data is being passed on command line interface. This utility allows serialization
/// of <see cref="IXunitSerializable"/> types to strings.
/// </summary>
internal class BasicSerializer : IXunitSerializationInfo
{
    // Stores (string representation, type) per key.
    private readonly Dictionary<string, (string str, Type type)> map = [];

    // Separator between key, type AQN and value in the dump format.
    // ':' is safe because AssemblyQualifiedName and converter output don't contain it.
    public const char Separator = ':';

    private string DumpToString(Type type)
    {
        using MemoryStream ms = new();
        using StreamWriter writer = new(ms);
        writer.WriteLine(type.FullName);
        foreach (KeyValuePair<string, (string str, Type t)> kv in this.map)
        {
            // Format: key:TypeAssemblyQualifiedName:value
            writer.WriteLine($"{kv.Key}{Separator}{kv.Value.t.AssemblyQualifiedName}{Separator}{kv.Value.str}");
        }

        writer.Flush();
        return Convert.ToBase64String(ms.ToArray());
    }

    private Type LoadDump(string dump)
    {
        byte[] data = Convert.FromBase64String(dump);

        using MemoryStream ms = new(data);
        using StreamReader reader = new(ms);
        Type type = Type.GetType(reader.ReadLine());
        for (string s = reader.ReadLine(); s != null; s = reader.ReadLine())
        {
            // Format: key:TypeAQN:value  (3 parts max to preserve ':' inside value)
            string[] parts = s.Split(Separator, 3);
            if (parts.Length == 3)
            {
                Type valueType = Type.GetType(parts[1]) ?? typeof(string);
                this.map[parts[0]] = (parts[2], valueType);
            }
            else if (parts.Length == 2)
            {
                // Legacy two-part format: key:value (no type info)
                this.map[parts[0]] = (parts[1], typeof(string));
            }
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

    /// <inheritdoc/>
    public void AddValue(string key, object value, Type type = null)
    {
        Guard.NotNull(key, nameof(key));
        if (value == null)
        {
            return;
        }

        type ??= value.GetType();
        this.map[key] = (TypeDescriptor.GetConverter(type).ConvertToInvariantString(value), type);
    }

    /// <inheritdoc/>
    public object GetValue(string key)
    {
        Guard.NotNull(key, nameof(key));

        if (!this.map.TryGetValue(key, out (string str, Type type) entry))
        {
            return null;
        }

        return TypeDescriptor.GetConverter(entry.type).ConvertFromInvariantString(entry.str);
    }
}
