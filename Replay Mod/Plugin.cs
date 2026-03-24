using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ReplayMod;
using ReplayMod.RecorderLifecycleBridge;
using ReplayMod.RecordManager;
using ReplayMod.ToolbarDrawer;
using ZeepSDK.UI;

namespace Replay_Mod
{ // will be replaced by assemblyName if desired
    [BepInPlugin("com.andme.replaymod", "ReplayMod", MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        private Harmony harmony;

        public static Plugin Instance { get; private set; }

        private MyToolbarDrawer _toolbarDrawer;

        private void Awake()
        {
            Instance = this;
            logger = Logger;

            harmony = new Harmony("com.andme.replaymod");
            harmony.PatchAll();

            logger.LogInfo("Plugin com.andme.replaymod is loaded!");

            RecorderLifecycleBridge.Initialize();
            _toolbarDrawer = new MyToolbarDrawer();
            UIApi.AddToolbarDrawer(_toolbarDrawer);
        }

        private void OnDestroy()
        {
            RecorderLifecycleBridge.Shutdown();
            RecordManager.Instance.StopRecording();

            UIApi.RemoveToolbarDrawer(_toolbarDrawer);

            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}
