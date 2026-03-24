using HarmonyLib;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_UndoRedo), "ApplyAfterState")]
    internal class LEV_UndoRedo_ApplyAfterStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(LEV_UndoRedo __instance)
        {
            RecordManager.RecordManager.Instance.CaptureRedo(__instance);
        }
    }
}
