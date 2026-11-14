using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// What a player can't ask the engine at runtime: the full Unity version with its revision (Application.unityVersion
    /// is only "6000.7.0b10") and when it was built. A build step (BagelBuildInfoStamp) writes them before every player
    /// build: the version only changes when the Editor does, and the date is cleared again once the build is done, so
    /// builds don't change the asset. Settings › About shows them.
    /// </summary>
    [CreateAssetMenu(fileName = "BagelBuildInfo", menuName = "Bagel/Build Info")]
    public class BagelBuildInfo : ScriptableObject
    {
        [Tooltip("The Editor's full version, e.g. 6000.7.0b10 (d95404e44f20). Written by the build.")]
        [SerializeField] string m_FullUnityVersion;

        [Tooltip("When the player was built, e.g. 2026-10-17 17:12 UTC. Written by the build, empty between builds.")]
        [SerializeField] string m_BuildDate;

        public string fullUnityVersion
        {
            get => m_FullUnityVersion;
            set => m_FullUnityVersion = value;
        }

        public string buildDate
        {
            get => m_BuildDate;
            set => m_BuildDate = value;
        }
    }
}
