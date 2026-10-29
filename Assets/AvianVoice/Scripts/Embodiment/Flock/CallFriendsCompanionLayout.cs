using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Player-view fan layout for Call Friends idle companions.
    /// </summary>
    public static class CallFriendsCompanionLayout
    {
        public struct PerchSettings
        {
            public float forwardMin;
            public float forwardMax;
            public float lateralSpread;
            public float heightMin;
            public float heightMax;
            public float fanAngleDegrees;
        }

        public static PerchSettings DefaultSettings => new()
        {
            forwardMin = 4f,
            forwardMax = 9f,
            lateralSpread = 5f,
            heightMin = 0.4f,
            heightMax = 1.6f,
            fanAngleDegrees = 150f
        };

        public static bool TryGetPlayerViewAnchor(out Transform anchor, out Vector3 flatForward)
        {
            anchor = null;
            flatForward = Vector3.forward;

            if (Camera.main != null)
                anchor = Camera.main.transform;
            else if (BirdSceneRoles.Player != null)
                anchor = BirdSceneRoles.Player;
            else
            {
                var xr = GameObject.Find("XR Origin");
                if (xr != null)
                    anchor = xr.transform;
            }

            if (anchor == null)
                return false;

            flatForward = anchor.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            else
                flatForward.Normalize();

            return true;
        }

        public static Vector3 SamplePerchedPosition(
            Transform anchor,
            Vector3 flatForward,
            int index,
            int total,
            int sessionSeed,
            PerchSettings settings)
        {
            var right = Vector3.Cross(Vector3.up, flatForward).normalized;
            var rng = new System.Random(sessionSeed + index * 7919);

            var t = total > 1 ? index / (float)(total - 1) : 0.5f;
            var baseAngle = Mathf.Lerp(-settings.fanAngleDegrees * 0.5f, settings.fanAngleDegrees * 0.5f, t);
            baseAngle += (float)(rng.NextDouble() * 30.0 - 15.0);

            var distance = Mathf.Lerp(settings.forwardMin, settings.forwardMax, (float)rng.NextDouble());
            var lateral = (float)(rng.NextDouble() * 2.0 - 1.0) * settings.lateralSpread * 0.35f;
            var height = Mathf.Lerp(settings.heightMin, settings.heightMax, (float)rng.NextDouble());

            var dir = Quaternion.Euler(0f, baseAngle, 0f) * flatForward;
            return anchor.position + dir * distance + right * lateral + Vector3.up * height;
        }

        public static Quaternion FacingToward(Transform anchor, Vector3 worldPos, int index, int sessionSeed)
        {
            var toPlayer = anchor.position - worldPos;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = -anchor.forward;

            var yawJitter = (index * 137 + sessionSeed) % 50 - 25f;
            return Quaternion.LookRotation(toPlayer.normalized, Vector3.up) * Quaternion.Euler(0f, yawJitter, 0f);
        }
    }
}
