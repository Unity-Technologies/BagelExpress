using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// How hot the device is, for the performance overlay and the Profiler (<see cref="DeviceThermalCounters"/>), from what
    /// each platform shares without a permission.
    /// Android: the battery's temperature (the sticky battery broadcast) and, from Android 10, the thermal status
    /// (PowerManager). iOS and macOS: the thermal state (NSProcessInfo; Apple shares no temperature). Elsewhere
    /// (Windows, the Web): nothing.
    /// </summary>
    public static class DeviceThermal
    {
        /// <summary>How worried to look: warm is where phones start slowing down, hot where they cap the frame rate.</summary>
        public enum Level
        {
            Unknown,
            Cool,
            Warm,
            Hot
        }

        public struct Reading
        {
            /// <summary>The battery's temperature in °C, or NaN.</summary>
            public float celsius;
            /// <summary>The platform's thermal state or status, lowercase ("nominal", "light"...), or null.</summary>
            public string state;
            /// <summary>
            /// The same as a number, or -1: Apple's NSProcessInfoThermalState (0 nominal to 3 critical) or Android's
            /// PowerManager thermal status (0 none to 6 shutdown).
            /// </summary>
            public int status;
            public Level level;
        }

        /// <summary>No reading: nothing this platform shares, or a call that failed.</summary>
        public static Reading none => new() { celsius = float.NaN, status = -1 };

        // NSProcessInfoThermalState: nominal, fair, serious (iOS caps the game at 30 fps), critical (15).
        static readonly string[] s_AppleStates = { "nominal", "fair", "serious", "critical" };
        static readonly Level[] s_AppleLevels = { Level.Cool, Level.Warm, Level.Hot, Level.Hot };
        // PowerManager.THERMAL_STATUS_*: 0 is none, and isn't shown.
        static readonly string[] s_AndroidStatuses = { null, "light", "moderate", "severe", "critical", "emergency", "shutdown" };

        /// <summary>The line in the overlay: "Battery 37.2 °C · light", "Thermal nominal", or empty.</summary>
        public static string Format(Reading reading)
        {
            if (!float.IsNaN(reading.celsius))
                return string.IsNullOrEmpty(reading.state) ? $"Battery {reading.celsius:0.0} °C" : $"Battery {reading.celsius:0.0} °C · {reading.state}";
            return string.IsNullOrEmpty(reading.state) ? string.Empty : $"Thermal {reading.state}";
        }

        /// <summary>An Apple thermal state (NSProcessInfoThermalState) as a reading.</summary>
        public static Reading FromAppleState(int state) => state >= 0 && state < s_AppleStates.Length
            ? new Reading { celsius = float.NaN, state = s_AppleStates[state], status = state, level = s_AppleLevels[state] }
            : none;

        /// <summary>An Android battery temperature (tenths of a degree, int.MinValue: unknown) and thermal status (-1: unknown).</summary>
        public static Reading FromAndroid(int tenthsCelsius, int status)
        {
            var reading = new Reading
            {
                celsius = tenthsCelsius == int.MinValue ? float.NaN : tenthsCelsius / 10f,
                state = status >= 0 && status < s_AndroidStatuses.Length ? s_AndroidStatuses[status] : null,
                status = status >= 0 ? status : -1,
            };
            reading.level = status >= 3 ? Level.Hot
                : status >= 1 ? Level.Warm
                : status == 0 || !float.IsNaN(reading.celsius) ? Level.Cool
                : Level.Unknown;
            return reading;
        }

        static bool s_Failed;

        /// <summary>This device's reading now. A few platform calls: the overlay reads it every couple of seconds.</summary>
        public static Reading Read()
        {
            if (s_Failed)
                return none;
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return FromAppleState(BagelThermalState());
#elif UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
                return FromAppleState(MacThermalState());
#elif UNITY_ANDROID && !UNITY_EDITOR
                return ReadAndroid();
#else
                return none;
#endif
            }
            catch (Exception e)
            {
                // Once: the overlay then stops asking.
                s_Failed = true;
                Debug.LogWarning("Bagel Express: the device's thermal state couldn't be read. " + e.Message);
                return none;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        // DeviceThermal.mm, compiled into the Xcode project.
        [DllImport("__Internal")]
        static extern int BagelThermalState();
#endif

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
        // [NSProcessInfo processInfo].thermalState through the Objective-C runtime: a macOS player can't compile a
        // .mm the way an iOS build does.
        const string k_ObjC = "/usr/lib/libobjc.A.dylib";

        [DllImport(k_ObjC)]
        static extern IntPtr objc_getClass(string name);

        [DllImport(k_ObjC)]
        static extern IntPtr sel_registerName(string name);

        [DllImport(k_ObjC, EntryPoint = "objc_msgSend")]
        static extern IntPtr SendReturningObject(IntPtr receiver, IntPtr selector);

        [DllImport(k_ObjC, EntryPoint = "objc_msgSend")]
        static extern long SendReturningInteger(IntPtr receiver, IntPtr selector);

        static int MacThermalState()
        {
            var processInfo = SendReturningObject(objc_getClass("NSProcessInfo"), sel_registerName("processInfo"));
            return processInfo == IntPtr.Zero ? -1 : (int)SendReturningInteger(processInfo, sel_registerName("thermalState"));
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject s_Filter;
        static AndroidJavaObject s_Power;
        static IntPtr s_RegisterReceiver;
        static IntPtr s_GetIntExtra;
        static bool s_AndroidReady;

        static Reading ReadAndroid()
        {
            var activity = UnityEngine.Android.AndroidApplication.currentActivity;
            if (!s_AndroidReady)
            {
                s_AndroidReady = true;
                s_Filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                var context = AndroidJNI.FindClass("android/content/Context");
                s_RegisterReceiver = AndroidJNI.GetMethodID(context, "registerReceiver",
                    "(Landroid/content/BroadcastReceiver;Landroid/content/IntentFilter;)Landroid/content/Intent;");
                AndroidJNI.DeleteLocalRef(context);
                var intent = AndroidJNI.FindClass("android/content/Intent");
                s_GetIntExtra = AndroidJNI.GetMethodID(intent, "getIntExtra", "(Ljava/lang/String;I)I");
                AndroidJNI.DeleteLocalRef(intent);
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                if (version.GetStatic<int>("SDK_INT") >= 29)
                    s_Power = activity.Call<AndroidJavaObject>("getSystemService", "power");
            }

            // No receiver: registerReceiver just returns the last battery broadcast, temperature in tenths of a degree.
            var tenths = int.MinValue;
            var battery = AndroidJNI.CallObjectMethod(activity.GetRawObject(), s_RegisterReceiver,
                new[] { new jvalue { l = IntPtr.Zero }, new jvalue { l = s_Filter.GetRawObject() } });
            if (battery != IntPtr.Zero)
            {
                var key = AndroidJNI.NewStringUTF("temperature");
                tenths = AndroidJNI.CallIntMethod(battery, s_GetIntExtra, new[] { new jvalue { l = key }, new jvalue { i = int.MinValue } });
                AndroidJNI.DeleteLocalRef(key);
                AndroidJNI.DeleteLocalRef(battery);
            }
            if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                AndroidJNI.ExceptionClear();

            var status = s_Power != null ? s_Power.Call<int>("getCurrentThermalStatus") : -1;
            return FromAndroid(tenths, status);
        }
#endif
    }
}
