using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hides small clutter (bottles, boxes, shelf items...) once it is further than <see cref="cullDistance"/>
/// from the camera, so the camera's far clip can stay long (big walls and buildings stay visible)
/// without paying for thousands of tiny far-away props.
///
/// Only the renderer is switched off - colliders, layers and physics are untouched, so bullets,
/// enemies and the camera behave exactly as before.
///
/// Use the "Collect Small Props" context menu (right-click the component header) to fill the list from
/// <see cref="roots"/>; re-run it after adding props to the scene.
/// </summary>
public class DistanceCuller : MonoBehaviour
{
    [Tooltip("Props further than this from the camera are not drawn.")]
    [SerializeField] float cullDistance = 16f;

    [Tooltip("Props only come back once they are this much closer than the cull distance (stops flickering at the edge).")]
    [SerializeField] float hysteresis = 1f;

    [Header("Collecting")]
    [Tooltip("Renderers whose biggest dimension is below this count as 'small props'.")]
    [SerializeField] float maxPropSize = 1.5f;
    [SerializeField] Transform[] roots;

    [Header("Collected renderers (filled by 'Collect Small Props')")]
    [SerializeField] Renderer[] renderers = new Renderer[0];

    [Tooltip("How many frames one full pass over the list takes (spreads the work out).")]
    [SerializeField, Min(1)] int framesPerPass = 3;

    bool[] visible;
    Camera cam;
    int cursor;

    void OnEnable()
    {
        visible = new bool[renderers.Length];
        for (int i = 0; i < visible.Length; i++)
            visible[i] = renderers[i] != null && renderers[i].enabled;
    }

    void OnDisable()
    {
        // leave everything visible if the culler is switched off
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = true;
    }

    void LateUpdate()
    {
        if (renderers.Length == 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float hideSqr = cullDistance * cullDistance;
        float showDist = Mathf.Max(0f, cullDistance - hysteresis);
        float showSqr = showDist * showDist;

        int perFrame = Mathf.CeilToInt(renderers.Length / (float)framesPerPass);
        for (int n = 0; n < perFrame; n++)
        {
            if (cursor >= renderers.Length) cursor = 0;
            int i = cursor++;
            Renderer r = renderers[i];
            if (r == null) continue;

            float sqr = r.bounds.SqrDistance(camPos);
            if (visible[i])
            {
                if (sqr > hideSqr) { r.enabled = false; visible[i] = false; }
            }
            else if (sqr < showSqr)
            {
                r.enabled = true; visible[i] = true;
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Collect Small Props")]
    public void CollectSmallProps()
    {
        var found = new List<Renderer>();
        foreach (Transform root in roots)
        {
            if (root == null) continue;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled) continue;
                Vector3 s = r.bounds.size;
                if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= maxPropSize) continue;
                if (IsDynamic(r.transform, root)) continue;
                found.Add(r);
            }
        }

        Undo.RecordObject(this, "Collect Small Props");
        renderers = found.ToArray();
        EditorUtility.SetDirty(this);
        Debug.Log($"[DistanceCuller] Collected {renderers.Length} small props.", this);
    }

    // things that get moved, animated, picked up or toggled by gameplay are left alone
    static bool IsDynamic(Transform t, Transform stopAt)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.GetComponent<Animator>() != null || p.GetComponent<Animation>() != null
                || p.GetComponent<Rigidbody>() != null || p.GetComponent<Interactable>() != null
                || p.GetComponent<UnityEngine.Playables.PlayableDirector>() != null
                || p.GetComponent<ParticleSystem>() != null || p.GetComponent<Light>() != null
                || p.GetComponent<SaveablePickup>() != null)
                return true;
            if (p == stopAt) break;
        }
        return false;
    }
#endif
}
