#if UNITY_EDITOR
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NavMeshTreeMaskBounds : MonoBehaviour
{
    [SerializeField]
    private Vector3 size = new Vector3(1f, 2f, 1f);

    [SerializeField]
    private Vector3 center = new Vector3(0f, 1f, 0f);

    public Vector3 Size => size;
    public Vector3 Center => center;

    private void OnDrawGizmosSelected()
    {
        var previousMatrix = Gizmos.matrix;
        var previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
#endif

