using UnityEngine;

namespace ReplayMod.RecordManager
{
    internal class CameraRecorder: MonoBehaviour
    {

        Vector3 lastPosition;
        Quaternion lastRotation;
        float lastRecordTime;

        [SerializeField] float positionThreshold = 0.01f;
        [SerializeField] float rotationThreshold = 0.5f;
        [SerializeField] float maxInterval = 0.1f;

        RecordManager recordManager;

        void Start()
        {
            recordManager = RecordManager.Instance;

            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastRecordTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            if (!recordManager.IsRecording || recordManager.CurrentSession == null)
                return;

            Vector3 currentPosition = transform.position;
            Quaternion currentRotation = transform.rotation;
            float currentTime = Time.realtimeSinceStartup;

            bool positionChanged = Vector3.Distance(lastPosition, currentPosition) > positionThreshold;
            bool rotationChanged = Quaternion.Angle(lastRotation, currentRotation) > rotationThreshold;
            bool timeExceeded = (currentTime - lastRecordTime) > maxInterval;

            if ((positionChanged || rotationChanged) && timeExceeded)
            {
                recordManager.CaptureCameraState(currentPosition, currentRotation);

                lastPosition = currentPosition;
                lastRotation = currentRotation;
                lastRecordTime = currentTime;
            }
        }
    }
}
