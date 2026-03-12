// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Globalization;
using Microsoft.DotNet.RemoteExecutor;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

/// <summary>
/// Allows the testing against specific feature sets.
/// </summary>
public static class FeatureTestRunner
{
    private static readonly char[] SplitChars = [',', ' '];

    /// <summary>
    /// Allows the deserialization of parameters passed to the feature test.
    /// <remark>
    /// <para>
    /// This is required because <see cref="RemoteExecutor"/> does not allow
    /// marshalling of fields so we cannot pass a wrapped <see cref="Action{T}"/>
    /// allowing automatic deserialization.
    /// </para>
    /// </remark>
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="value">The string value to deserialize.</param>
    /// <returns>The <see cref="T"/> value.</returns>
    public static T DeserializeForXunit<T>(string value)
        where T : IXunitSerializable
        => BasicSerializer.Deserialize<T>(value);

    /// <summary>
    /// Allows the deserialization of types implementing <see cref="IConvertible"/>
    /// passed to the feature test.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="value">The string value to deserialize.</param>
    /// <returns>The <typeparamref name="T"/> value.</returns>
    public static T Deserialize<T>(string value)
        where T : IConvertible
        => (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <param name="action">The test action to run.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    public static void RunWithHwIntrinsicsFeature(
        Action action,
        HwIntrinsics intrinsics)
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action();
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <param name="action">
    /// The test action to run.
    /// The parameter passed will be a string representing the currently testing <see cref="HwIntrinsics"/>.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    public static void RunWithHwIntrinsicsFeature(
        Action<string> action,
        HwIntrinsics intrinsics)
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    intrinsic.Key.ToString(),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(intrinsic.Key.ToString());
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    /// <param name="serializable">The value to pass as a parameter to the test action.</param>
    public static void RunWithHwIntrinsicsFeature<T>(
        Action<string> action,
        HwIntrinsics intrinsics,
        T serializable)
        where T : IXunitSerializable
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    BasicSerializer.Serialize(serializable),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(BasicSerializer.Serialize(serializable));
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    /// <param name="serializable">The value to pass as a parameter to the test action.</param>
    public static void RunWithHwIntrinsicsFeature<T>(
        Action<string, string> action,
        HwIntrinsics intrinsics,
        T serializable)
        where T : IXunitSerializable
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    BasicSerializer.Serialize(serializable),
                    intrinsic.Key.ToString(),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(BasicSerializer.Serialize(serializable), intrinsic.Key.ToString());
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <typeparam name="T2">The addition type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    /// <param name="arg1">The value to pass as a parameter to the test action.</param>
    /// <param name="arg2">The second value to pass as a parameter to the test action.</param>
    public static void RunWithHwIntrinsicsFeature<T, T2>(
        Action<string, string> action,
        HwIntrinsics intrinsics,
        T arg1,
        T2 arg2)
        where T : IXunitSerializable
        where T2 : IXunitSerializable
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    BasicSerializer.Serialize(arg1),
                    BasicSerializer.Serialize(arg2),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(BasicSerializer.Serialize(arg1), BasicSerializer.Serialize(arg2));
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    /// <param name="arg1">The value to pass as a parameter to the test action.</param>
    /// <param name="arg2">The second value to pass as a parameter to the test action.</param>
    public static void RunWithHwIntrinsicsFeature<T>(
        Action<string, string> action,
        HwIntrinsics intrinsics,
        T arg1,
        string arg2)
        where T : IXunitSerializable
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    BasicSerializer.Serialize(arg1),
                    arg2,
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(BasicSerializer.Serialize(arg1), arg2);
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="serializable">The value to pass as a parameter to the test action.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    public static void RunWithHwIntrinsicsFeature<T>(
        Action<string> action,
        T serializable,
        HwIntrinsics intrinsics)
        where T : IConvertible
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    serializable.ToString(),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(serializable.ToString());
            }
        }
    }

    /// <summary>
    /// Runs the given test <paramref name="action"/> within an environment
    /// where the given <paramref name="intrinsics"/> features.
    /// </summary>
    /// <typeparam name="T">The type of argument.</typeparam>
    /// <param name="action">The test action to run.</param>
    /// <param name="arg0">The value to pass as a parameter #0 to the test action.</param>
    /// <param name="arg1">The value to pass as a parameter #1 to the test action.</param>
    /// <param name="intrinsics">The intrinsics features.</param>
    public static void RunWithHwIntrinsicsFeature<T>(
        Action<string, string> action,
        T arg0,
        T arg1,
        HwIntrinsics intrinsics)
        where T : IConvertible
    {
        if (!RemoteExecutor.IsSupported)
        {
            return;
        }

        foreach (KeyValuePair<HwIntrinsics, string> intrinsic in intrinsics.ToFeatureKeyValueCollection())
        {
            ProcessStartInfo processStartInfo = new();
            if (intrinsic.Key != HwIntrinsics.AllowAll)
            {
                processStartInfo.Environment[$"DOTNET_{intrinsic.Value}"] = "0";

                RemoteExecutor.Invoke(
                    action,
                    arg0.ToString(),
                    arg1.ToString(),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
            else
            {
                // Since we are running using the default architecture there is no
                // point creating the overhead of running the action in a separate process.
                action(arg0.ToString(), arg1.ToString());
            }
        }
    }

    internal static Dictionary<HwIntrinsics, string> ToFeatureKeyValueCollection(this HwIntrinsics intrinsics)
    {
        // Loop through and translate the given values into COMPlus equivalents
        Dictionary<HwIntrinsics, string> features = [];
        foreach (string intrinsic in intrinsics.ToString("G").Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
        {
            HwIntrinsics key = Enum.Parse<HwIntrinsics>(intrinsic);
            switch (intrinsic)
            {
                case nameof(HwIntrinsics.AllowAll):

                    // Not a COMPlus value. We filter in calling method.
                    features.Add(key, nameof(HwIntrinsics.AllowAll));
                    break;

                default:
                    features.Add(key, intrinsic.Replace("Disable", "Enable"));
                    break;
            }
        }

        return features;
    }
}

/// <summary>
/// See <see href="https://github.com/dotnet/runtime/blob/58601ba7da092fe82bb71d087d30df95472968b6/src/coreclr/jit/jitconfigvalues.h#L315"/>
/// </summary>
[Flags]
#pragma warning disable RCS1135 // Declare enum member with zero value (when enum has FlagsAttribute).
public enum HwIntrinsics : long
#pragma warning restore RCS1135 // Declare enum member with zero value (when enum has FlagsAttribute).
{
    // Use flags so we can pass multiple values without using params.
    // Don't base on 0 or use inverse for All as that doesn't translate to string values.
    DisableHWIntrinsic = 1L << 0,
    DisableSSE42 = 1L << 1,
    DisableAVX = 1L << 2,
    DisableAVX2 = 1L << 3,
    DisableAVX512 = 1L << 4,
    DisableAVX512v2 = 1L << 5,
    DisableAVX512v3 = 1L << 6,
    DisableAVX10v1 = 1L << 7,
    DisableAVX10v2 = 1L << 8,
    DisableAPX = 1L << 9,
    DisableAES = 1L << 10,
    DisableAVX512VP2INTERSECT = 1L << 11,
    DisableAVXIFMA = 1L << 12,
    DisableAVXVNNI = 1L << 13,
    DisableAVXVNNIINT = 1L << 14,
    DisableGFNI = 1L << 15,
    DisableSHA = 1L << 16,
    DisableVAES = 1L << 17,
    DisableWAITPKG = 1L << 18,
    DisableX86Serialize = 1 << 19,
    // Arm64
    DisableArm64Aes = 1L << 20,
    DisableArm64Atomics = 1L << 21,
    DisableArm64Crc32 = 1L << 22,
    DisableArm64Dczva = 1L << 23,
    DisableArm64Dp = 1L << 24,
    DisableArm64Rdm = 1L << 25,
    DisableArm64Sha1 = 1L << 26,
    DisableArm64Sha256 = 1L << 27,
    DisableArm64Sve = 1L << 28,
    DisableArm64Sve2 = 1L << 29,
    // RISC-V64
    DisableRiscV64Zba = 1L << 30,
    DisableRiscV64Zbb = 1L << 31,
    AllowAll = 1L << 32,
}
