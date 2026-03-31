using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_MoveCamera), "IsCursorInGameView")]
    internal class LEV_MoveCamera_IsCursorInGameViewPatch
    {
        [HarmonyPostfix]
        private static void Postfix(LEV_MoveCamera __instance, ref bool __result)
        {
            if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
            {
                __instance.isInGameView = true;
                __result = true;
            }
        }
    }
}
