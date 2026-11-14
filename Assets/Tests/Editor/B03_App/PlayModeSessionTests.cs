using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Bagel.B03_App
{
    /// <summary>
    /// In the Editor, every Play session plays like a first launch, and the Editor keeps no save outside Play mode
    /// (<see cref="BagelSave"/>).
    /// </summary>
    public class PlayModeSessionTests
    {
        const string k_PlayedGame = "{\"version\":1,\"coins\":4321,\"tutorialDone\":true}";

        [UnityTest]
        public IEnumerator APlaySessionStartsWithoutASaveAndLeavesNone()
        {
            var muted = UnityEditor.EditorUtility.audioMasterMute;
            UnityEditor.EditorUtility.audioMasterMute = true;
            PlayerPrefs.SetString(BagelSave.gameKey, k_PlayedGame);
            PlayerPrefs.SetString(BagelSave.settingsKey, JsonUtility.ToJson(new BagelSave.Settings { season = 2 }));

            yield return new EnterPlayMode();
            Assert.That(PlayerPrefs.HasKey(BagelSave.gameKey), Is.False, "the game starts over: the tutorial plays again");
            Assert.That(PlayerPrefs.HasKey(BagelSave.settingsKey), Is.False, "every setting starts at its default");
            Assert.That(SettingsPaneManager.seasonIndex, Is.Zero);

            // What the game saves while it plays.
            PlayerPrefs.SetString(BagelSave.gameKey, k_PlayedGame);
            yield return new ExitPlayMode();
            Assert.That(PlayerPrefs.HasKey(BagelSave.gameKey), Is.False, "forgotten as Play mode ends");
            UnityEditor.EditorUtility.audioMasterMute = muted;
        }
    }
}
