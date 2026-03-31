using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_MoveCamera), "TranslateCamera")]
    internal class LEV_MoveCamera_TranslateCameraPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (PlaybackManager.PlaybackManager.Instance.IsPlaying && PlaybackManager.PlaybackManager.Instance.followCamera)
            {
                return false;
            }
            return true;
        }
    }
}
