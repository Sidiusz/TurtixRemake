using UnityEngine;

namespace Turtix.Unity
{
    /// Multi-layer parallax background.
    /// - UNIFORM-scaled (no distortion) so the square covers ~the world height + a small margin
    ///   (slightly bigger than the play area -> you see the top edge at the world's top).
    /// - Vertical: world-anchored (fixed), so edges show at world extremes.
    /// - Horizontal: parallax follow of the camera (backmost moves more here per the art).
    /// - Clouds: tile horizontally + auto-scroll independently.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Range(0f, 1f)] public float parallaxFactor = 0.2f; // horizontal on-screen motion vs camera
        public float autoScrollX = 0f;                      // px/s independent scroll (clouds: negative = left)
        public bool tileX = false;                          // infinite horizontal tiling (clouds)
        public float worldHeight = 1536f;
        public float worldWidth = 5376f;
        public float coverMargin = 1.08f;                   // bg is this * world height (a bit bigger)
        public float verticalAnchor = 0.5f;                 // 0=bottom .. 1=top of world for the bg center

        private Camera cam;
        private SpriteRenderer sr;
        private float camStartX;
        private float spriteW, spriteH;   // native (PPU=1) size
        private float scroll;
        private float z;

        void OnEnable()
        {
            sr = GetComponent<SpriteRenderer>();
            z = transform.position.z;
            if (sr.sprite != null) { spriteW = sr.sprite.bounds.size.x; spriteH = sr.sprite.bounds.size.y; }
            Acquire();
        }

        void Acquire()
        {
            cam = Camera.main;
            if (cam != null) camStartX = cam.transform.position.x;
        }

        void LateUpdate()
        {
            if (cam == null) { Acquire(); if (cam == null) return; }
            if (spriteH <= 0f) { if (sr.sprite == null) return; spriteW = sr.sprite.bounds.size.x; spriteH = sr.sprite.bounds.size.y; }

            // uniform cover scale: height = world height * margin (square stays square)
            float scale = (worldHeight * coverMargin) / spriteH;
            transform.localScale = new Vector3(scale, scale, 1f);

            if (tileX)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                float viewW = cam.orthographic ? cam.orthographicSize * cam.aspect * 2f : 2000f;
                float needNativeW = (viewW * 1.8f) / scale;     // enough native width to cover after scaling
                sr.size = new Vector2(Mathf.Max(needNativeW, spriteW * 3f), spriteH);
            }

            if (Application.isPlaying) scroll += autoScrollX * Time.deltaTime;
            float scaledW = spriteW * scale;
            if (scaledW > 0f) scroll = Mathf.Repeat(scroll, scaledW);   // wrap so clouds loop seamlessly

            float camDX = cam.transform.position.x - camStartX;
            float x = cam.transform.position.x - camDX * parallaxFactor + (tileX ? scroll : 0f);
            float y = worldHeight * verticalAnchor;     // world-anchored vertical (fixed)
            transform.position = new Vector3(x, y, z);
        }
    }
}
