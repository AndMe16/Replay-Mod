using HarmonyLib;

namespace ReplayMod.InternalPatches
{
    [HarmonyPatch(typeof(LEV_MoveCamera), "UpdateGameView")]
    internal class LEV_MoveCamera_UpdateGameViewPatch
    {
        [HarmonyPostfix]
        private static void Postfix(LEV_MoveCamera __instance)
        {
            if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
            {
                __instance.isInGameView = true;
            }
        }
    }
}

