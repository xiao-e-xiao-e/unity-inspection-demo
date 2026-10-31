using UnityEngine;

namespace Inspection
{
    public sealed class SurfaceProbe : MonoBehaviour
    {
        public Camera viewCamera;
        public InspectionHud hud;
        public LayerMask surfaceMask = 1 << 8;
        public Material markerMaterial;
        public float maxDistance = 30f;
        public bool HasHit { get; private set; }
        public Vector3 HitPosition { get; private set; }
        public string TargetName { get; private set; } = "尚未选择";
        public int HitCount { get; private set; }
        private GameObject marker;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Clear();
            if (!Input.GetMouseButtonDown(0) || Input.GetMouseButton(1)) return;
            if (hud.ContainsScreenPoint(Input.mousePosition)) return;
            if (TryPick(viewCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
                ShowHit(hit);
        }

        public bool TryPick(Ray ray, out RaycastHit hit)
        {
            return Physics.Raycast(ray, out hit, maxDistance, surfaceMask, QueryTriggerInteraction.Ignore);
        }

        public void ShowHit(RaycastHit hit)
        {
            var target = hit.collider.GetComponentInParent<InspectionTarget>();
            TargetName = target != null ? target.displayName : hit.collider.name;
            HitPosition = hit.point; // Store the actual surface coordinate, not the visual offset.
            HasHit = true;
            HitCount++;
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Raycast hit preview (not saved)";
                marker.layer = 2; // Ignore Raycast; preview must never intercept a later selection.
                var collider = marker.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                marker.transform.localScale = Vector3.one * 0.16f;
            }
            marker.transform.position = hit.point + hit.normal * 0.08f;
        }

        public void Clear()
        {
            if (marker != null) Destroy(marker);
            marker = null;
            HasHit = false;
            TargetName = "尚未选择";
        }
    }
}
