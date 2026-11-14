using NUnit.Framework;
using UnityEditor;

namespace Bagel.B03_App
{
    /// <summary>
    /// Auto mode's frame caps for a hot iPhone are the ones Player Settings › iOS › Adjust iOS FPS based on thermal
    /// state sets: a player can't read those settings, so <see cref="AutoPerformance"/> has its own copy.
    /// </summary>
    public class AutoPerformanceEditorTests
    {
        [Test]
        public void AHotIPhonesCapsAreThePlayerSettings()
        {
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));

            Assert.That(settings.FindProperty("adjustIOSFPSUsingThermalState").boolValue, Is.True);
            Assert.That(settings.FindProperty("thermalStateSeriousIOSFPS").intValue, Is.EqualTo(AutoPerformance.iosSeriousFps));
            Assert.That(settings.FindProperty("thermalStateCriticalIOSFPS").intValue, Is.EqualTo(AutoPerformance.iosCriticalFps));
        }
    }
}
