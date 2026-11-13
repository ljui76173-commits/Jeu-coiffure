using UnityEngine;

namespace SalonCoiffure
{
    /// <summary>Caméra qui tourne autour de la tête de la cliente. yaw = 0 : de face.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 target;
        public float yaw, pitch = 8f, distance = 0.8f;
        public float minDist = 0.32f, maxDist = 3.2f;

        Vector3 curTarget;
        float cy, cp, cd;
        bool init;

        public void Set(Vector3 t, float yawDeg, float pitchDeg, float dist, bool snap)
        {
            target = t; yaw = yawDeg; pitch = pitchDeg; distance = Mathf.Clamp(dist, minDist, maxDist);
            if (snap || !init) Snap();
        }

        public void Snap()
        {
            curTarget = target; cy = yaw; cp = pitch; cd = distance;
            init = true;
            Apply();
        }

        public void Orbit(float dYaw, float dPitch)
        {
            yaw += dYaw;
            pitch = Mathf.Clamp(pitch + dPitch, -25f, 80f);
        }

        public void Zoom(float factor)
        {
            distance = Mathf.Clamp(distance * factor, minDist, maxDist);
        }

        void LateUpdate()
        {
            if (!init) Snap();
            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            curTarget = Vector3.Lerp(curTarget, target, k);
            cy = Mathf.LerpAngle(cy, yaw, k);
            cp = Mathf.Lerp(cp, pitch, k);
            cd = Mathf.Lerp(cd, distance, k);
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(cp, cy + 180f, 0f);
            transform.position = curTarget + rot * new Vector3(0, 0, -cd);
            transform.rotation = rot;
        }

        public float CurrentDistance { get { return cd; } }
    }
}
