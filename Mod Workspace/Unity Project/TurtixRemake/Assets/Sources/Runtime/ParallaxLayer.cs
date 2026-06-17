using UnityEngine;

namespace Turtix.Unity
{
    /// Simple (non-tiled) parallax background layer. One sprite, scaled to fill the camera
    /// viewport, positioned in world space and blended toward the camera by a parallax factor:
    ///   factor 0 = stays centred on the camera (static on screen = infinitely far)
    ///   factor 1 = stuck to a world anchor (moves fully with the world = foreground)
    /// Separate X/Y factors. Back layer: factorX ~0 (static horizontally), factorY a bit more.
    /// Near layer: both larger. Clouds also auto-drift horizontally.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Range(0f, 1f)] public float parallaxX = 0f;
        [Range(0f, 1f)] public float parallaxY = 0.2f;
        public float autoScrollX = 0f;        // px/s independent drift (clouds)
        public float coverMargin = 1.12f;     // fill viewport * this (room for parallax shift)
        public Vector2 anchor = new Vector2(2688f, 768f);  // world anchor (scene center)

        private Camera cam;
        private SpriteRenderer sr;
        private float cellW, cellH;
        private float scroll;
        private float z;
        private bool ready;

        void OnEnable()
        {
            sr = GetComponent<SpriteRenderer>();
            z = transform.position.z;
            Setup();
        }

        void Setup()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr.sprite == null) { ready = false; return; }
            sr.drawMode = SpriteDrawMode.Simple;
            cellW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            cellH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
            cam = Camera.main;
            ready = cellW > 0f && cellH > 0f;
        }

        void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }
            if (cam == null) { cam = Camera.main; if (cam == null) return; }

            // fill the viewport (+margin) — Simple stretch, no tiling.
            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 1536f;
            float viewW = cam.orthographic ? viewH * cam.aspect : 2048f;
            transform.localScale = new Vector3(viewW * coverMargin / cellW,
                                               viewH * coverMargin / cellH, 1f);

            if (Application.isPlaying) scroll += autoScrollX * Time.deltaTime;

            float camX = cam.transform.position.x;
            float camY = cam.transform.position.y;
            float x = camX * (1f - parallaxX) + anchor.x * parallaxX + scroll;
            float y = camY * (1f - parallaxY) + anchor.y * parallaxY;
            transform.position = new Vector3(x, y, z);
        }
    }
}
