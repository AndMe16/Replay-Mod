using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_ToolSwitch), "Update")]
    internal class LEV_ToolSwitch_UpdatePatch
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
