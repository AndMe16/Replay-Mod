using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_UndoRedo), "ApplyBeforeState")]
    internal class LEV_UndoRedo_ApplyBeforeStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(LEV_UndoRedo __instance)
        {
            RecordManager.RecordManager.Instance.CaptureUndo(__instance);
        }
    }
}
