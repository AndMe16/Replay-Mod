using Replay_Mod;
using UnityEngine;

namespace ReplayMod.PauseMenuHandler
{
    public class PauseMenuHandler : MonoBehaviour
    {
        private LEV_LevelEditorCentral central;

        void Start()
        {
            central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();
        }

        void Update()
        {
            if (central == null) return;
            PlaybackManager.PlaybackManager.Instance.UpdateRealtimePlayback();

            if ((central.input.Escape.buttonDown || central.input.MenuPause.buttonDown) &&
                (central.pause.CurrentOpenSettingsUI == null || !central.pause.CurrentOpenSettingsUI.IsOpen) &&
                !central.unsavedContentPopup.IsOpen)
            {

                Plugin.logger.LogInfo("Escape or MenuPause button pressed. Opening custom pause menu.");
                central.pause.EnablePauseMenu();
            }
        }
    }
}
