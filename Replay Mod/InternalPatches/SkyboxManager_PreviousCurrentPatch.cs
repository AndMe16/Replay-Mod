using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(SkyboxManager), "PreviousCurrent")]
    internal class SkyboxManager_PreviousCurrentPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
            {
                return false;
            }
            return true;
        }
    }
}
