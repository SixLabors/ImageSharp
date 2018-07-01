using System;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    public static partial class Telemetry
    {
        private static bool enabled = false;
        static Telemetry()
        {
            //we try to load our app insights provider and expose it here
            nullProvider = new NullProvider();
            try
            {
                var type = Type.GetType("SixLabors.Telemetry.TelemetryProvider, SixLabors.Telemetry", false);
                if (type != null)
                {
                    var provider = Activator.CreateInstance(type) as TelemetryProviderBase;
                    if (provider != null)
                    {
                        RegisterProvider(provider);
                    }
                }
            }
            catch
            {
                // no-op we just ignore this error as its not a critical thing and probably just cause some dependency hasn't been updateded
            }

            Enable();
        }

        public static void Disable()
        {
            // this call will disable the provider at all and no-op any calls
            activeProvider = nullProvider;
            enabled = false;
        }

        public static void Enable()
        {
            // this call will disable the provider at all and no-op any calls
            if (wrappedProvider.InnerProvider == null)
            {
                activeProvider = nullProvider;
            }
            else
            {
                activeProvider = wrappedProvider;
            }
            enabled = true;
        }

        internal static TelemetryProviderBase Provider => activeProvider;
        private static TelemetryProviderBase activeProvider;
        private static SafeWrappingProvider wrappedProvider = new SafeWrappingProvider();
        private static NullProvider nullProvider;

        internal static void RegisterProvider(TelemetryProviderBase provider)
        {
            // use guard 
            if (provider == null) throw new ArgumentNullException();

            if (wrappedProvider.InnerProvider == null)
            {
                wrappedProvider.InnerProvider = provider;
            }
            else if (wrappedProvider.InnerProvider is AggregateProvider ap)
            {
                ap.RegisterProvider(provider);
            }
            else
            {
                wrappedProvider.InnerProvider = new AggregateProvider(wrappedProvider.InnerProvider, provider);
            }

            if (enabled)
            {
                if (wrappedProvider.InnerProvider == null)
                {
                    activeProvider = nullProvider;
                }
                else
                {
                    activeProvider = wrappedProvider;
                }
            }
        }
    }
}
