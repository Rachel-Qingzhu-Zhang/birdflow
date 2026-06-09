using AvianVoice.Core;
using AvianVoice.Embodiment;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public static class BreathSyncEvaluator
    {
        public static float Evaluate(BreathFrame user, BirdFriendDemonstrator friend)
        {
            if (friend == null)
                return 0f;

            var targetDepth = friend.GetTargetDepth();
            var depthError = Mathf.Abs(user.depth - targetDepth);
            var depthScore = 1f - Mathf.Clamp01(depthError);

            var phaseMatch = user.isInhaling == friend.IsInhaling ? 1f : 0f;
            return depthScore * 0.7f + phaseMatch * 0.3f;
        }
    }
}
