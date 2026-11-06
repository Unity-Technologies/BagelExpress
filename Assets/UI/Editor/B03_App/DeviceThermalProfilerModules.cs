using Unity.Profiling.Editor;

namespace Bagel
{
    // The Profiler window's charts for DeviceThermalCounters and PerfSweep's circuit, live or in a saved capture: one
    // module each, as their scales differ (about 30–45 °C, 0–6, 1–21). A module whose counter a capture doesn't have stays
    // empty.

    /// <summary>The battery's temperature in °C (Android), from <see cref="DeviceThermalCounters"/>.</summary>
    [ProfilerModuleMetadata("Device Temperature")]
    public sealed class DeviceTemperatureProfilerModule : ProfilerModule
    {
        public DeviceTemperatureProfilerModule() : base(new[] { new ProfilerCounterDescriptor(DeviceThermalCounters.temperatureName, DeviceThermalCounters.categoryName) }) { }
    }

    /// <summary>
    /// The thermal state (iOS and macOS: 0 nominal to 3 critical) or status (Android: 0 none to 6 shutdown), from
    /// <see cref="DeviceThermalCounters"/>.
    /// </summary>
    [ProfilerModuleMetadata("Thermal Status")]
    public sealed class ThermalStatusProfilerModule : ProfilerModule
    {
        public ThermalStatusProfilerModule() : base(new[] { new ProfilerCounterDescriptor(DeviceThermalCounters.thermalStatusName, DeviceThermalCounters.categoryName) }) { }
    }

    /// <summary>
    /// The stop a PerfSweep circuit capture is at (BE_PERF_SWEEP=circuit with BE_PERF_PROFILE): 1, 2... in the order
    /// the circuit visits them; a "PerfSweep.Stop" sample in the stop's first frame names it.
    /// </summary>
    [ProfilerModuleMetadata("Circuit Stop")]
    public sealed class CircuitStopProfilerModule : ProfilerModule
    {
        public CircuitStopProfilerModule() : base(new[] { new ProfilerCounterDescriptor("Circuit Stop", DeviceThermalCounters.categoryName) }) { }
    }
}
