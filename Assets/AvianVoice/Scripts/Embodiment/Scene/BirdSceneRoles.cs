using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Wires the single scene-placed PlayerParrot for Birdy tasks. A second parrot, if present, is hidden
    /// and Friend stays null. Does not duplicate or reposition birds.
    /// </summary>
    public static class BirdSceneRoles
    {
        public const string FriendName = "FriendParrot";
        public const string PlayerName = "PlayerParrot";
        public const string PerchName = "Cylinder";
        public const int MirrorLayer = 9;
        public const int MirrorDisplayLayer = 10;
        public const int UiLayer = 11;

        public static Transform Friend { get; private set; }
        public static Transform Player { get; private set; }

        public static bool TryGetFlockOrbitCenter(out Vector3 center)
        {
            if (Player != null)
            {
                center = Player.position;
                return true;
            }

            center = default;
            return false;
        }

        static bool birdsConfigured;
        static bool friendConfigured;

        public static void ResetSession()
        {
            birdsConfigured = false;
            Friend = null;
            Player = null;
        }

        public static void EnsureSetup()
        {
            if (!ResolveSceneBirds())
                return;

            if (!birdsConfigured)
                ConfigureBirdComponents();

            StripBodyCueSpheres();
            FinalizeScenePlacement();

            CommitPoses(Player);

            LogLayoutDiagnostics();
        }

        static bool ResolveSceneBirds()
        {
            Friend = null;
            Player = null;

            foreach (var role in Object.FindObjectsByType<BirdSceneRole>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (role == null)
                    continue;

                if (role.Role == BirdSceneRole.RoleKind.Friend)
                    Friend = role.transform;
                else
                    Player = role.transform;
            }

            if (Friend == null)
                Friend = FindBirdTransform(FriendName);
            if (Player == null)
                Player = FindBirdTransform(PlayerName);

            if (Player == null)
            {
                var legacy = FindLegacyParrot();
                if (legacy != null)
                {
                    legacy.name = PlayerName;
                    Player = legacy;
                    EnsureSceneRoleComponent(Player, BirdSceneRole.RoleKind.Player);
                }
            }

            if (Player == null && Friend != null)
            {
                Player = Friend;
                Friend = null;
            }

            if (Player == null)
            {
                Debug.LogError("[AvianVoice] BirdSceneRoles: Avian_Lab needs one parrot named PlayerParrot.");
                return false;
            }

            // Friend stays in the scene but hidden until a task asks for it.
            if (Friend != null && Friend != Player)
                Friend.gameObject.SetActive(false);

            Player.gameObject.SetActive(true);
            return true;
        }

        static void LogLayoutDiagnostics()
        {
            var playerSmr = GetBodyRenderer(Player);
            var playerBounds = playerSmr != null ? playerSmr.bounds.size : Vector3.zero;

            Debug.Log(
                "[AvianVoice] BirdSceneRoles: single parrot ready. " +
                $"Player '{Player.name}'@{Player.position} bounds={playerBounds}");
        }

        static void ConfigureBirdComponents()
        {
            SetupBird(Player, RoleMarker.Role.Player);
            birdsConfigured = true;
        }

        static void SetupBird(Transform bird, RoleMarker.Role role)
        {
            var guard = bird.GetComponent<WorldPlacedBird>() ?? bird.gameObject.AddComponent<WorldPlacedBird>();
            guard.ConfigureForFixedWorldPlacement();

            var animator = bird.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(animator);
            }

            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(bird);
            ParrotEmbodimentWiring.HideRigControlMeshes(bird);

            if (role == RoleMarker.Role.Friend)
            {
                var playerBreath = bird.GetComponent<ParrotBreathEmbodiment>();
                if (playerBreath != null)
                    Object.Destroy(playerBreath);
                if (bird.GetComponent<FriendBreathVisual>() == null)
                    bird.gameObject.AddComponent<FriendBreathVisual>();
                if (bird.GetComponent<FriendTeachingCue>() == null)
                    bird.gameObject.AddComponent<FriendTeachingCue>();
            }
            else
            {
                var friendVisual = bird.GetComponent<FriendBreathVisual>();
                if (friendVisual != null)
                    Object.Destroy(friendVisual);
                if (bird.GetComponent<ParrotBreathEmbodiment>() == null)
                    bird.gameObject.AddComponent<ParrotBreathEmbodiment>();
            }

            var pin = bird.GetComponent<ParrotMainCtrlPin>() ?? bird.gameObject.AddComponent<ParrotMainCtrlPin>();
            pin.SetPinEnabled(true);
        }

        static void StripBodyCueSpheres()
        {
            StripBodyCueSpheres(Player);
        }

        static void StripBodyCueSpheres(Transform bird)
        {
            if (bird == null)
                return;

            foreach (var child in bird.GetComponentsInChildren<Transform>(true))
            {
                if (child.name is "FriendBreathCue" or "PlayerMarker" or "FriendMarker")
                    Object.Destroy(child.gameObject);
            }

            foreach (var marker in bird.GetComponents<RoleMarker>())
                Object.Destroy(marker);
            foreach (var cue in bird.GetComponents<FriendTeachingCue>())
                Object.Destroy(cue);
        }

        static void FinalizeScenePlacement()
        {
            RefreshPinPoses(Player);
            SetLayerRecursive(Player.gameObject, MirrorLayer);
            EnsurePlayerBirdVisible();
            LabBirdSceneLighting.Ensure();
        }

        public static void SetFriendVisible(bool visible)
        {
            if (!ResolveSceneBirds() && Friend == null)
                return;
            if (Friend == null || Friend == Player)
                return;

            if (!visible)
            {
                Friend.GetComponent<FriendBreathVisual>()?.ClearPerformancePose();
                Friend.gameObject.SetActive(false);
                return;
            }

            Friend.gameObject.SetActive(true);
            if (!friendConfigured)
            {
                SetupBird(Friend, RoleMarker.Role.Friend);
                foreach (var cue in Friend.GetComponents<FriendTeachingCue>())
                    Object.Destroy(cue);
                friendConfigured = true;
            }

            SetLayerRecursive(Friend.gameObject, MirrorLayer);
            ParrotEmbodimentWiring.HideRigControlMeshes(Friend);
            PlaceFriendBesidePlayer();
            Friend.GetComponent<WorldPlacedBird>()?.CommitWorldPose();
        }

        static void PlaceFriendBesidePlayer()
        {
            if (Friend == null || Player == null)
                return;

            var flat = Friend.position - Player.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.2f && flat.sqrMagnitude < 1.4f)
                return;

            var right = Player.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.right;
            right.Normalize();
            Friend.SetPositionAndRotation(Player.position + right * 0.72f, Player.rotation);
            Friend.GetComponent<WorldPlacedBird>()?.SaveWorldPose();
        }

        public static void EnsurePlayerBirdVisible()
        {
            if (Player == null)
                return;

            Player.gameObject.SetActive(true);
            ParrotEmbodimentWiring.HideRigControlMeshes(Player);
            foreach (var renderer in Player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                if (renderer.gameObject.name is "PlayerMarker" or "FriendMarker")
                    continue;
                if (ParrotEmbodimentWiring.IsRigControlObject(renderer.gameObject))
                    continue;

                renderer.enabled = true;
                foreach (var mat in renderer.materials)
                {
                    if (mat == null)
                        continue;

                    if (mat.HasProperty("_BaseColor"))
                    {
                        var color = mat.GetColor("_BaseColor");
                        color.a = 1f;
                        mat.SetColor("_BaseColor", color);
                    }
                    else
                    {
                        var color = mat.color;
                        color.a = 1f;
                        mat.color = color;
                    }
                }
            }

            ParrotEmbodimentWiring.HideRigControlMeshes(Player);
        }

        static void RefreshPinPoses(Transform bird)
        {
            var pin = bird?.GetComponent<ParrotMainCtrlPin>();
            pin?.CaptureRestPose();
        }

        static SkinnedMeshRenderer GetBodyRenderer(Transform bird)
        {
            if (bird == null)
                return null;

            SkinnedMeshRenderer best = null;
            var bestVolume = 0f;
            foreach (var smr in bird.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || !smr.enabled)
                    continue;

                smr.updateWhenOffscreen = true;
                var volume = smr.bounds.size.sqrMagnitude;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = smr;
                }
            }

            return best;
        }

        static void CommitPoses(Transform bird)
        {
            bird?.GetComponent<WorldPlacedBird>()?.CommitWorldPose();
        }

        static void EnsureSceneRoleComponent(Transform bird, BirdSceneRole.RoleKind role)
        {
            if (bird == null)
                return;

            var marker = bird.GetComponent<BirdSceneRole>() ?? bird.gameObject.AddComponent<BirdSceneRole>();
            marker.SetRole(role);
        }

        static Transform FindLegacyParrot()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Parrot")
                    return t;
            }

            return null;
        }

        static Transform FindBirdTransform(string objectName)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == objectName)
                    return t;
            }

            return null;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
