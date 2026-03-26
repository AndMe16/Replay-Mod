using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReplayMod.PlaybackManager
{
    internal class PlaybackController: MonoBehaviour
    {
        void Update()
        {
            PlaybackManager.Instance.UpdateRealtimePlayback();
        }

    }
}
