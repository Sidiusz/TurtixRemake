using UnityEngine;

namespace Turtix.Unity
{
    /// Camera-relative parallax background layer (skybox-style depth).
    /// Three layers per world: back (far, moves least), clouds (middle, also auto-drifts),
    /// near (moves a bit more). Each layer follows the camera by parallaxFactor:
    ///   factor 0 = locked to camera (infinitely far, never moves on screen)
    ///   factor 1 = moves with the world (like foreground)
    /// Small factor => the layer appears far away and barely shifts as the player moves.
    /// Layers tile horizontally (and cover vertically) so they always fill the viewport.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Range(0f, 1f)] public float parallaxFactor = 0.2f;   // horizontal depth (0=far/locked .. 1=world)
        [Range(0f, 1f)] public float parallaxFactorY = 0f;    // vertical depth (0=fixed band)
        public float autoScrollX = 0f;                        // px/s independent drift (clouds)
        public float tileWorldW = 1024f;                      // one tile width in world px (cell*scale)
        public float tileWorldH = 1024f;
        public float worldHeight = 1536f;
        public float bandCenterY = 768f;                      // fixed vertical center of the band (world px)

        private Camera cam;
        private SpriteRenderer sr;
        private float cellW, cellH;
        private float camStartX, camStartY;
        private float baseX, baseY;
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
            cellW = sr.sprite.bounds.size.x;
            cellH = sr.sprite.bounds.size.y;
            if (cellW <= 0f || cellH <= 0f) { ready = false; return; }

            float sx = tileWorldW / cellW;
            float sy = tileWorldH / cellH;
            transform.localScale = new Vector3(sx, sy, 1f);

            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            AcquireCam();
            ready = true;
        }

        void AcquireCam()
        {
            cam = Camera.main;
            if (cam == null) return;
            camStartX = cam.transform.position.x;
            camStartY = cam.transform.position.y;
            baseX = cam.transform.position.x;
            baseY = bandCenterY;
        }

        void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }
            if (cam == null) { AcquireCam(); if (cam == null) return; }

            // size the tiled quad to cover the viewport (+ margin) at all times.
            float viewW = cam.orthographic ? cam.orthographicSize * cam.aspect * 2f : 2048f;
            float viewH = cam.orthographic ? cam.orthographicSize * 2f : 1536f;
            float sx = transform.localScale.x, sy = transform.localScale.y;
            sr.size = new Vector2((viewW + 2f * tileWorldW) / sx, (Mathf.Max(viewH, worldHeight) + 2f * tileWorldH) / sy);

            if (Application.isPlaying) scroll += autoScrollX * Time.deltaTime;
            scroll = Mathf.Repeat(scroll, tileWorldW);

            float camDX = cam.transform.position.x - camStartX;
            float camDY = cam.transform.position.y - camStartY;
            // keep the tiled quad centred on the camera, shifted by the parallax remainder so
            // content drifts slower than the world; wrap keeps it seamless.
            float x = cam.transform.position.x - Mathf.Repeat(camDX * (1f - parallaxFactor) - scroll, tileWorldW);
            float y = parallaxFactorY > 0f ? (baseY + camDY * parallaxFactorY) : bandCenterY;
            transform.position = new Vector3(x, y, z);
        }
    }
}
