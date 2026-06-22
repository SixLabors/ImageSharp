// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections;
using System.Reflection;
using System.Text;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class ObuPrettyPrint
{
    private static readonly char[] Spaces = "                                                             ".ToCharArray();

    public static string PrettyPrintProperties(object obj, int indent = 0)
    {
        StringBuilder builder = new();
        builder.Append(obj.GetType().Name);
        builder.AppendLine("{");
        indent += 2;
        MemberInfo[] properties = obj.GetType().FindMembers(MemberTypes.Property, BindingFlags.Instance | BindingFlags.Public, null, null);
        foreach (MemberInfo member in properties)
        {
            builder.Append(Spaces, 0, indent);
            if (member is PropertyInfo property)
            {
                builder.Append(property.Name);
                builder.Append(" = ");
                object value = property.GetValue(obj) ?? "NULL";
                PrettyPrintValue(builder, value, indent);
            }
        }

        indent -= 2;
        builder.Append(Spaces, 0, indent);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void PrettyPrintValue(StringBuilder builder, object value, int indent)
    {
        if (value.GetType() == typeof(string))
        {
            builder.AppendLine(value.ToString());
        }
        else if (value.GetType().IsArray)
        {
            builder.AppendLine("[");
            indent += 2;
            builder.Append(Spaces, 0, indent);
            Type elementType = value.GetType().GetElementType();
            IList list = value as IList;
            foreach (object item in list)
            {
                PrettyPrintValue(builder, item, indent);
            }

            indent -= 2;
            builder.Append(Spaces, 0, indent);
            builder.AppendLine("]");
        }
        else if (value.GetType().IsClass)
        {
            builder.AppendLine(PrettyPrintProperties(value, indent));
        }
        else
        {
            builder.AppendLine(value.ToString());
        }
    }
}
