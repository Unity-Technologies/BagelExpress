// Bagel Express — the thermal state for the performance overlay (DeviceThermal.cs), iOS only: the system's
// NSProcessInfoThermalState (0 nominal, 1 fair, 2 serious, 3 critical). Apple shares no temperature.

#import <Foundation/Foundation.h>

extern "C" int BagelThermalState()
{
    return (int)[NSProcessInfo processInfo].thermalState;
}
