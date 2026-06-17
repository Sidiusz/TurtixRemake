using UnityEngine;

namespace Turtix.Unity
{
    /// Orthographic camera that follows a target (the local player) with smoothing,
    /// clamped to the level bounds. Pixel-space: orthographicSize is in pixels/2.
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smooth = 8f;
        public Vector2 offset = new Vector2(0f, 64f);

        // level bounds in world (pixel) space; camera center clamped so view stays inside.
        public bool useBounds = true;
        public Rect levelBounds = new Rect(0, 0, 5376, 1536);

        private Camera cam;

        void Awake() { cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = transform.position;
            Vector3 want = new Vector3(target.position.x + offset.x, target.position.y + offset.y, p.z);

            if (useBounds && cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                if (levelBounds.width >= 2 * halfW)
                    want.x = Mathf.Clamp(want.x, levelBounds.xMin + halfW, levelBounds.xMax - halfW);
                else
                    want.x = levelBounds.center.x;
                if (levelBounds.height >= 2 * halfH)
                    want.y = Mathf.Clamp(want.y, levelBounds.yMin + halfH, levelBounds.yMax - halfH);
                else
                    want.y = levelBounds.center.y;
            }

            transform.position = Vector3.Lerp(p, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }
    }
}
