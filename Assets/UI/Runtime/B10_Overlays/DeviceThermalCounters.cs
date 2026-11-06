using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Profiling;

namespace Bagel
{
    /// <summary>
    /// How hot the device is, in the Unity Profiler: two counters in the Scripts category, read from
    /// <see cref="DeviceThermal"/> once a second while the Profiler records (an attached Profiler or a player's binary log,
    /// in development builds and the Editor's Play mode), so they're saved with the capture.
    /// <list type="bullet">
    /// <item><see cref="temperatureName"/>: the battery's temperature in °C (Android).</item>
    /// <item><see cref="thermalStatusName"/>: the platform's thermal state (iOS and macOS: 0 nominal, 1 fair, 2 serious,
    /// 3 critical) or thermal status (Android 10 and later: 0 none, 1 light, 2 moderate, 3 severe, 4 critical,
    /// 5 emergency, 6 shutdown).</item>
    /// </list>
    /// A counter the platform can't fill is never created, so a capture has only the ones it measured (the Web and
    /// Windows have neither). The Profiler window shows them in the Device Temperature and Thermal Status modules
    /// (<c>DeviceThermalProfilerModules</c>, in this project); elsewhere, add them in its Profiler Module Editor.
    /// </summary>
    public static class DeviceThermalCounters
    {
        /// <summary>
        /// Scripts, always recorded with the CPU: the Profiler drops the values of a category that isn't enabled, and a
        /// new one isn't.
        /// </summary>
        public const string categoryName = "Scripts";
        public const string temperatureName = "Battery Temperature (°C)";
        public const string thermalStatusName = "Thermal Status";

        /// <summary>Seconds between readings: each one calls the platform (JNI on Android).</summary>
        public const float interval = 1f;

#if ENABLE_PROFILER
        static ProfilerCounterValue<float> s_Temperature;
        static ProfilerCounterValue<int> s_Status;
        static bool s_HasTemperature;
        static bool s_HasStatus;
        static float s_Next;

        // A step at the end of each frame, without a GameObject. Enter Play Mode Options skip the domain reload in this
        // project, and the player loop may outlive a Play session: add the step once.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            s_Next = 0f;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList == null)
                return;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(PostLateUpdate))
                    continue;
                var steps = loop.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                if (Array.Exists(steps, step => step.type == typeof(DeviceThermalCounters)))
                    return;
                Array.Resize(ref steps, steps.Length + 1);
                steps[^1] = new PlayerLoopSystem { type = typeof(DeviceThermalCounters), updateDelegate = Sample };
                loop.subSystemList[i].subSystemList = steps;
                PlayerLoop.SetPlayerLoop(loop);
                return;
            }
        }

        static void Sample()
        {
            if (!Profiler.enabled)
                return;
            var now = Time.realtimeSinceStartup;
            if (now < s_Next)
                return;
            s_Next = now + interval;

            var reading = DeviceThermal.Read();
            if (!float.IsNaN(reading.celsius))
            {
                if (!s_HasTemperature)
                {
                    s_Temperature = new ProfilerCounterValue<float>(ProfilerCategory.Scripts, temperatureName, ProfilerMarkerDataUnit.Undefined);
                    s_HasTemperature = true;
                }
                s_Temperature.Value = reading.celsius;
            }
            if (reading.status >= 0)
            {
                if (!s_HasStatus)
                {
                    s_Status = new ProfilerCounterValue<int>(ProfilerCategory.Scripts, thermalStatusName, ProfilerMarkerDataUnit.Count);
                    s_HasStatus = true;
                }
                s_Status.Value = reading.status;
            }
        }
#endif
    }
}
