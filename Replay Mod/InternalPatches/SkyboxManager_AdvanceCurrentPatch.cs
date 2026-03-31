using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(SkyboxManager), "AdvanceCurrent")]
    internal class SkyboxManager_AdvanceCurrentPatch
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
