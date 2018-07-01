using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace SixLabors.Telemetry
{
    internal sealed class TelemetryProvider : TelemetryProviderBase
    {
        private readonly TelemetryClient client;

        public TelemetryProvider()
        {
            var config = new TelemetryConfiguration("86a8b580-17ac-4648-be54-a962bcbf74a2");

            config
                .TelemetryProcessorChainBuilder
                .Use(p => new MachineMetricsProcessors(p))
                .Build();

            this.client = new Microsoft.ApplicationInsights.TelemetryClient(config);
#if !NETSTANDARD1_3
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                this.client.Flush();
            };
#endif
        }

        public override void Event(Func<TelemetryDetails> detailsFunc)
        {
            var details = detailsFunc();
            this.client.TrackEvent(details.Operation, details.Properties, details.Metrics);
        }

        public override void Exception(Exception ex, Func<TelemetryDetails> detailsFunc)
        {
            var details = detailsFunc();
            details.Properties["Library"] = details.Library;
            this.client.TrackException(ex, details.Properties, details.Metrics);
        }

        public override IDisposable Operation(Func<TelemetryDetails> detailsFunc)
        {
            var previousActivityLibrary = Activity.Current?.GetBaggageItem("Library");
            var previousOperationType = Activity.Current?.GetBaggageItem("OperationType");
            var details = detailsFunc();

            // we are transitioning from one library to another 
            // lets tag it up so we can filter them out later as required
            bool startInLib = (details.Library != previousActivityLibrary) || (details.OperationType != previousOperationType);

            var activity = new Activity(details.Operation);
            activity.AddBaggage("Library", details.Library);
            activity.AddBaggage("OperationType", details.OperationType);

            var operation = this.client.StartOperation<RequestTelemetry>(activity);
            operation.Telemetry.Source = details.Library;
            if (details.Properties != null)
            {
                foreach (KeyValuePair<string, string> d in details.Properties)
                {
                    operation.Telemetry.Properties.Add(d.Key, d.Value);
                }
            }
            if (details.Metrics != null)
            {
                foreach (KeyValuePair<string, double> d in details.Metrics)
                {
                    operation.Telemetry.Metrics.Add(d.Key, d.Value);
                }
            }

            if (startInLib)
            {
                operation.Telemetry.Metrics.Add("LibraryTransition", 1);
            }

            return Wrap(details, operation);
        }

        private ErrorCapturingTelemetry<TOperationTelemetry> Wrap<TOperationTelemetry>(TelemetryDetails details, IOperationHolder<TOperationTelemetry> telemetry)
            where TOperationTelemetry : OperationTelemetry
        {
            return new ErrorCapturingTelemetry<TOperationTelemetry>(this.client, details, telemetry);
        }

        private struct ErrorCapturingTelemetry<TOperationTelemetry> : IDisposable
            where TOperationTelemetry : OperationTelemetry
        {
            private readonly TelemetryClient client;
            private readonly TelemetryDetails details;
            private readonly IOperationHolder<TOperationTelemetry> telemetry;

            public ErrorCapturingTelemetry(TelemetryClient client, TelemetryDetails details, IOperationHolder<TOperationTelemetry> telemetry)
            {
                this.client = client;
                this.details = details;
                this.telemetry = telemetry;
            }

            public void Dispose()
            {
                if (Marshal.GetExceptionCode() != 0)
                {
                    this.telemetry.Telemetry.Success = false;
                }

                this.telemetry?.Dispose();
            }
        }
    }
    internal class MachineMetricsProcessors : ITelemetryProcessor
    {
        private readonly double hardwardVectors;
        private readonly ITelemetryProcessor next;

        public MachineMetricsProcessors(ITelemetryProcessor next)
        {
            this.hardwardVectors = System.Numerics.Vector.IsHardwareAccelerated ? 1 : 0;
            this.next = next;
        }

        public void Process(ITelemetry item)
        {
            if (item is ISupportMetrics hasMetrics)
            {
                hasMetrics.Metrics.Add("Hardware Vectors", hardwardVectors);
            }

            if (item is RequestTelemetry request)
            {
                if (request.Metrics.Remove("LibraryTransition")) // remove and then only log trantion items
                {
                    next?.Process(item);

                }
            }
            else
            {
                next?.Process(item);
            }

        }
    }
}
