using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Bagel.B03_App
{
    /// <summary>
    /// The Game view's size menu has the project's phones (<see cref="GameViewDeviceSizes"/>) in each group, once each.
    /// </summary>
    public class GameViewDeviceSizesTests
    {
        [Test]
        public void EachGroupHasItsPhonesOnceAfterApplying()
        {
            GameViewDeviceSizes.Apply();
            Assert.That(GameViewDeviceSizes.Apply(), Is.Zero, "a second pass adds nothing");

            foreach (var group in GameViewDeviceSizes.groups)
            {
                var names = GameViewDeviceSizes.NamesIn(group);
                foreach (var (name, _, _) in GameViewDeviceSizes.SizesOf(group))
                    Assert.That(names.Count(n => n == name), Is.EqualTo(1), $"{group}: {name}");
            }
        }

        [Test]
        public void IPhonesGoToIOSGalaxiesToAndroidAndAllToStandalone()
        {
            Assert.That(GameViewDeviceSizes.SizesOf(GameViewSizeGroupType.iOS).Select(s => s.name), Is.All.StartsWith("iPhone"));
            Assert.That(GameViewDeviceSizes.SizesOf(GameViewSizeGroupType.Android).Select(s => s.name), Is.All.StartsWith("Galaxy"));
            Assert.That(GameViewDeviceSizes.SizesOf(GameViewSizeGroupType.Standalone).Length, Is.EqualTo(GameViewDeviceSizes.sizes.Length + 1));
            Assert.That(GameViewDeviceSizes.sizes.Select(s => s.name), Is.Unique);
        }
    }
}
