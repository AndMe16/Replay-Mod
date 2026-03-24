using HarmonyLib;
using Replay_Mod;

namespace ReplayMod.InternalPatches
{

    [HarmonyPatch(typeof(LEV_UndoRedo), "SomethingChanged")]
    internal class LEV_UndoRedo_SomethingChangedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(LEV_UndoRedo __instance, Change_Collection whatChanged, string source)
        {
            RecordManager.RecordManager.Instance.CaptureSomethingChanged(__instance, whatChanged, source);
        }
    }
}
