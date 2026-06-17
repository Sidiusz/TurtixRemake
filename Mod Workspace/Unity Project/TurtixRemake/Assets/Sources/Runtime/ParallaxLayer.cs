using UnityEngine;

namespace Turtix.Unity
{
    /// World-fixed tiled background layer — 1:1 with the original t2dTileLayer.
    /// Engine truth (W1_01 inspect dump): bg layers are NOT mounted to the camera and have
    /// NO horizontal parallax. They sit at world pos (0,0), size = whole scene (5376x1536),
    /// wrap X+Y, and the camera just scrolls over them. The small cell image is rendered at
    /// 4x (engine tileSize = cellSize * 4) and tiled across the scene.
    /// Only the clouds layer auto-pans (~10 px/s X via panPosition; engine AutoPan field = 0,
    /// the drift is applied per-frame by plrLevel). panY is a fixed vertical texture offset.
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        public float worldWidth = 5376f;
        public float worldHeight = 1536f;
        public float tileWorldW = 1024f;   // engine tileSizeX (= cellW * 4)
        public float tileWorldH = 1024f;   // engine tileSizeY (= cellH * 4)
        public float panX = 0f;            // engine panPositionX (px)
        public float panY = 0f;            // engine panPositionY (px) — W1 bg = -495
        public float autoScrollX = 0f;     // px/s; clouds = 10, others 0

        private SpriteRenderer sr;
        private float cellW, cellH;
        private float scroll;
        private bool ready;

        void OnEnable()
        {
            sr = GetComponent<SpriteRenderer>();
            Setup();
        }

        void Setup()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr.sprite == null) { ready = false; return; }

            cellW = sr.sprite.bounds.size.x;   // native px at PPU=1
            cellH = sr.sprite.bounds.size.y;
            if (cellW <= 0f || cellH <= 0f) { ready = false; return; }

            // scale so one tile == engine tileSize; tile the sprite across world + 1-tile margin.
            float sx = tileWorldW / cellW;
            float sy = tileWorldH / cellH;
            transform.localScale = new Vector3(sx, sy, 1f);

            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2((worldWidth + 2f * tileWorldW) / sx,
                                  (worldHeight + 2f * tileWorldH) / sy);
            ready = true;
            Apply();
        }

        void Apply()
        {
            // world-fixed; pan offset wraps within one tile so the seam never shows.
            float ox = Mathf.Repeat(panX + scroll, tileWorldW);
            float oy = Mathf.Repeat(panY, tileWorldH);
            transform.position = new Vector3(worldWidth * 0.5f - ox,
                                             worldHeight * 0.5f - oy,
                                             transform.position.z);
        }

        void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }
            if (Application.isPlaying && autoScrollX != 0f)
            {
                scroll += autoScrollX * Time.deltaTime;
                Apply();
            }
        }
    }
}
