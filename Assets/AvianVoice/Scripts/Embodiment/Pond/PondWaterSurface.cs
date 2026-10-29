using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Replaces the default plane with an organic pond mesh and keeps material settings in sync.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class PondWaterSurface : MonoBehaviour
    {
        [SerializeField]
        int segments = 72;

        [SerializeField]
        float radiusX = 11f;

        [SerializeField]
        float radiusZ = 9f;

        [SerializeField]
        float edgeIrregularity = 0.24f;

        [SerializeField]
        int shapeSeed = 7;

        [SerializeField]
        bool rebuildOnAwake = true;

        Mesh pondMesh;

        public void RebuildMesh()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
                return;

            if (pondMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(pondMesh);
                else
                    DestroyImmediate(pondMesh);
            }

            pondMesh = PondWaterMeshBuilder.CreatePondMesh(
                segments,
                radiusX,
                radiusZ,
                edgeIrregularity,
                shapeSeed);
            filter.sharedMesh = pondMesh;
        }

        void Awake()
        {
            if (rebuildOnAwake)
                RebuildMesh();
        }

        void OnDestroy()
        {
            if (pondMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(pondMesh);
            else
                DestroyImmediate(pondMesh);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            RebuildMesh();
        }
#endif
    }
}
