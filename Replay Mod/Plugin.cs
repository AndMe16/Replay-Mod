using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ReplayMod;

namespace Replay_Mod
{ // will be replaced by assemblyName if desired
    [BepInPlugin("com.andme.replaymod", "ReplayMod", MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        private Harmony harmony;

        public static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            logger = Logger;

            harmony = new Harmony("com.andme.replaymod");
            harmony.PatchAll();

            logger.LogInfo("Plugin com.andme.replaymod is loaded!");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}
