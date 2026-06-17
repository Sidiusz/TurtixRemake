#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtix.Unity
{
    public class TurtixLevelImporter : EditorWindow
    {
        private const int Tile = 128;
        private const string DataRoot = "Assets/Sources/GeneratedData";
        private const string ScenesRoot = "Assets/Sources/Scenes";

        // Background constants. tileScale/cloud-drift from the engine dump; parallax factors
        // and band placement are tunable (adjust on the ParallaxLayer components to taste).
        private const float BgTileScale = 4f;        // bg cell rendered at 4x
        private const float BgCloudScrollX = 10f;    // clouds auto-pan px/s
        private const float BgBandCenter = 0.5f;     // vertical band center as fraction of scene height

        // Parallax depth by engine layer order: higher order = further back = moves least.
        private static float BgParallaxFactor(int order)
        {
            switch (order)
            {
                case 8: return 0.12f;   // back (far)
                case 7: return 0.25f;   // clouds (middle)
                case 6: return 0.40f;   // near
                default: return 0.30f;
            }
        }

        [System.Serializable]
        public class ImageMapDB
        {
            public ImageMapData[] maps;
        }

        [System.Serializable]
        public class ImageMapData
        {
            public string name;
            public string png;
            public int cellW;
            public int cellH;
            public string mode;
        }

        [System.Serializable]
        public class AnimDB { public AnimData[] anims; }

        [System.Serializable]
        public class AnimData
        {
            public string name;
            public string imageMap;
            public int[] frames;
            public int cycle;     // 1 = loop
            public float time;    // total seconds for the full frame list
        }

        [System.Serializable]
        public class TileLevel
        {
            public string file;
            public int[] scene;
            public TileLayer[] layers;
        }

        [System.Serializable]
        public class TileLayer
        {
            public int order;
            public int tileW;
            public int tileH;
            public int cols;
            public int rows;
            public bool background;
            public bool collision;
            public TileCell[] cells;   // SPARSE: only filled cells
        }

        [System.Serializable]
        public class TileCell
        {
            public int x;
            public int y;
            public int frame;
            public string img;
        }

        [System.Serializable]
        public class ObjectLevel
        {
            public string level;
            public int[] scene;
            public ObjectData[] objects;
        }

        [System.Serializable]
        public class ObjectData
        {
            public int typeId;
            public string template;
            public int x;
            public int y;
            public float sx;
            public float sy;
            public int layer;
            public string img;
            public int frame;
            public bool isPlayer;
            public bool isPortal;
        }

        [System.Serializable]
        private class ObjectLevelWrapper
        {
            public ObjectLevel level;
        }

        private string[] levelNames;
        private int selectedLevel;
        private bool YFlip = true;   // engine +Y is down; flip to Unity +Y up
        private readonly Dictionary<string, Sprite> spriteCache = new();

        [MenuItem("Turtix/Import Level")]
        public static void ShowWindow()
        {
            GetWindow<TurtixLevelImporter>("Turtix Level Importer");
        }

        private void OnEnable()
        {
            string tilesDir = Path.Combine(DataRoot, "tiles");
            if (Directory.Exists(tilesDir))
            {
                levelNames = Directory.GetFiles(tilesDir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(x => x)
                    .ToArray();
            }
            else
            {
                levelNames = new string[0];
            }
        }

        private void OnGUI()
        {
            if (levelNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No tile JSON found in Assets/Sources/GeneratedData/tiles/", MessageType.Error);
                return;
            }

            selectedLevel = EditorGUILayout.Popup("Level", selectedLevel, levelNames);
            YFlip = EditorGUILayout.Toggle("Flip Y (engine Y-down)", YFlip);
            if (GUILayout.Button("Import Selected Level"))
            {
                ImportLevel(levelNames[selectedLevel]);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Import All 60 Levels (scenes)"))
            {
                ImportAllLevels();
            }
        }

        private void ImportAllLevels()
        {
            foreach (var name in levelNames)
            {
                ImportLevel(name, true);
            }
        }

        private void ImportLevel(string levelName, bool saveScene = false)
        {
            string path = Path.Combine(DataRoot, "tiles", levelName + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError("Missing " + path);
                return;
            }

            string json = "{\"level\":" + File.ReadAllText(path) + "}";
            var wrapper = JsonUtility.FromJson<LevelWrapper>(json);
            TileLevel level = wrapper.level;

            Scene scene;
            if (saveScene)
            {
                string scenePath = Path.Combine(ScenesRoot, levelName + ".unity");
                scenePath = scenePath.Replace("\\", "/");
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = SceneManager.GetActiveScene();
            }

            // Clear any previous import of this level in the active scene (avoid stacked
            // duplicate roots -> double player, doubled static+animated layers).
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == levelName)
                    Object.DestroyImmediate(existing);

            var root = new GameObject(levelName);
            Undo.RegisterCreatedObjectUndo(root, "Import Turtix Level");

            for (int i = 0; i < level.layers.Length; i++)
            {
                var layer = level.layers[i];
                string kind = layer.collision ? "Collision" : (layer.background ? "BG" : "Tiles");
                var layerGo = new GameObject($"Layer_{kind}_order{layer.order}");
                layerGo.transform.SetParent(root.transform, false);
                BuildLayer(layer, layerGo);
            }

            BuildObjects(levelName, level, root);

            if (saveScene)
            {
                string scenePath = Path.Combine(ScenesRoot, levelName + ".unity").Replace("\\", "/");
                EditorSceneManager.SaveScene(scene, scenePath, true);
                Debug.Log("Saved scene " + scenePath);
            }
            else
            {
                Selection.activeGameObject = root;
                Debug.Log($"Imported {levelName}: {level.layers.Length} layers, scene={level.scene[0]}x{level.scene[1]}");
            }
        }

        [System.Serializable]
        private class LevelWrapper
        {
            public TileLevel level;
        }

        // Collision polygon for a collision-tile frame, derived from the Collisions.png art:
        // the opaque shape per cell (full square / slope triangle) IS the collider. Unity
        // auto-generates a physics shape from the sprite alpha; we scale it to the tile.
        // Returns null only if no physics shape -> caller falls back to a full box.
        private Vector2[] SlopePoly(int frame, float w, float h)
        {
            Sprite s = GetSprite("i1", frame);   // i1 = Collisions.png shape key
            if (s == null || s.GetPhysicsShapeCount() <= 0) return null;
            var pts = new List<Vector2>();
            s.GetPhysicsShape(0, pts);
            if (pts.Count < 3) return null;
            float bx = s.bounds.size.x, by = s.bounds.size.y;
            if (bx <= 0f || by <= 0f) return null;
            float rx = w / bx, ry = h / by;      // sprite cell -> layer tile size
            var arr = new Vector2[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                arr[i] = new Vector2(pts[i].x * rx, pts[i].y * ry);
            return arr;
        }

        private PhysicsMaterial2D groundMat;
        private PhysicsMaterial2D GetGroundMaterial()
        {
            if (groundMat != null) return groundMat;
            string path = "Assets/Sources/Runtime/GroundNoFriction.physicsMaterial2D";
            groundMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (groundMat == null)
            {
                groundMat = new PhysicsMaterial2D("GroundNoFriction") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(groundMat, path);
            }
            return groundMat;
        }

        private void BuildLayer(TileLayer layer, GameObject parent)
        {
            int rows = layer.rows;
            int sortOrder = -layer.order;   // engine order: high=back. -order => back is most negative.

            // collision layer: invisible. Full-box cells (frame 0/1) merge into a single
            // CompositeCollider2D so flat runs are seamless (no snagging on tile seams).
            // Slope cells (frame >= 4) get their own collider shape.
            if (layer.collision)
            {
                var body = parent.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var comp = parent.AddComponent<CompositeCollider2D>();
                comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                comp.sharedMaterial = GetGroundMaterial();

                foreach (var cell in layer.cells)
                {
                    var col = new GameObject($"col_{cell.x}_{cell.y}_f{cell.frame}");
                    col.transform.SetParent(parent.transform, false);
                    float cxp = cell.x * layer.tileW + layer.tileW * 0.5f;
                    float cyp = (rows - 1 - cell.y) * layer.tileH + layer.tileH * 0.5f;
                    col.transform.localPosition = new Vector3(cxp, cyp, 0);

                    Vector2[] slope = SlopePoly(cell.frame, layer.tileW, layer.tileH);
                    if (slope != null)
                    {
                        var pc = col.AddComponent<PolygonCollider2D>();
                        pc.points = slope;
                        pc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    }
                    else
                    {
                        var bc = col.AddComponent<BoxCollider2D>();
                        bc.size = new Vector2(layer.tileW, layer.tileH);
                        bc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    }
                }
                return;
            }

            // Background layers — camera-relative parallax (skybox depth).
            // Three layers per world: back (far, lowest order-number-on-screen = highest engine
            // order, moves least), clouds (middle, auto-drift), near (moves a bit more).
            // Each tiles to fill the viewport; parallaxFactor sets the depth (small = far).
            if (layer.background)
            {
                float sceneW = layer.cols * layer.tileW;
                float sceneH = layer.rows * layer.tileH > 0 ? layer.rows * layer.tileH : layer.tileH;
                foreach (var cell in layer.cells)
                {
                    ImageMapData bgMap = FindImageMap(cell.img);
                    Sprite sprite = GetBackgroundSprite(cell.img);
                    if (sprite == null || bgMap == null) continue;

                    var bgo = new GameObject($"bg_{cell.img}_order{layer.order}");
                    bgo.transform.SetParent(parent.transform, false);

                    var bsr = bgo.AddComponent<SpriteRenderer>();
                    bsr.sprite = sprite;
                    bsr.sortingOrder = sortOrder;

                    bool isClouds = bgMap.png.Contains("_C_");   // *_Background_*_C_* = clouds
                    var px = bgo.AddComponent<ParallaxLayer>();
                    px.worldHeight = sceneH;
                    px.tileWorldW = bgMap.cellW * BgTileScale;   // cell rendered at 4x
                    px.tileWorldH = bgMap.cellH * BgTileScale;
                    px.bandCenterY = sceneH * BgBandCenter;      // vertical band placement
                    px.parallaxFactor = BgParallaxFactor(layer.order);
                    px.parallaxFactorY = 0f;                     // fixed vertical band
                    px.autoScrollX = isClouds ? BgCloudScrollX : 0f;
                }
                return;
            }

            foreach (var cell in layer.cells)
            {
                Sprite sprite = GetSprite(cell.img, cell.frame);
                if (sprite == null) continue;

                var go = new GameObject($"{cell.img}_{cell.frame}_{cell.x}_{cell.y}");
                go.transform.SetParent(parent.transform, false);

                float px = cell.x * layer.tileW + layer.tileW * 0.5f;
                float py = (rows - 1 - cell.y) * layer.tileH + layer.tileH * 0.5f;
                go.transform.localPosition = new Vector3(px, py, 0);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortOrder;

                // t2d tile layers scale each imageMap frame to FILL the tile slot
                // (e.g. 96px source -> 128px cell). Scale to fit.
                Vector2 nat = sprite.bounds.size;   // world size at PPU=1 == native px
                if (nat.x > 0 && nat.y > 0)
                    go.transform.localScale = new Vector3(layer.tileW / nat.x, layer.tileH / nat.y, 1);
            }
        }

        // Background sprite = whole image (Single mode), wrap Repeat, so SpriteRenderer
        // drawMode=Tiled repeats it across the scene instead of stretching.
        private Sprite GetBackgroundSprite(string imageMapName)
        {
            string key = "BG_" + imageMapName;
            if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            ImageMapData map = FindImageMap(imageMapName);
            if (map == null || !File.Exists(map.png)) { Debug.LogError("BG imageMap missing " + imageMapName); return null; }

            if (!slicedPngs.Contains(map.png))
            {
                slicedPngs.Add(map.png);
                var ti = AssetImporter.GetAtPath(map.png) as TextureImporter;
                if (ti != null)
                {
                    var tis = new TextureImporterSettings();
                    ti.ReadTextureSettings(tis);
                    bool dirty = ti.spriteImportMode != SpriteImportMode.Single ||
                                 ti.wrapMode != TextureWrapMode.Repeat ||
                                 !Mathf.Approximately(ti.spritePixelsPerUnit, 1f) ||
                                 tis.spriteMeshType != SpriteMeshType.FullRect;
                    if (dirty)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.spritePixelsPerUnit = 1;
                        ti.wrapMode = TextureWrapMode.Repeat;   // seamless tiling
                        ti.mipmapEnabled = false;
                        tis.spriteMeshType = SpriteMeshType.FullRect;  // required for Tiled drawMode
                        ti.SetTextureSettings(tis);
                        EditorUtility.SetDirty(ti);
                        ti.SaveAndReimport();
                    }
                }
            }
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(map.png);
            if (s != null) spriteCache[key] = s;
            return s;
        }

        private void BuildObjects(string levelName, TileLevel level, GameObject root)
        {
            string path = Path.Combine(DataRoot, "objects", levelName + ".json").Replace("\\", "/");
            if (!File.Exists(path)) { Debug.LogWarning("No objects json: " + path); return; }

            string json = "{\"level\":" + File.ReadAllText(path) + "}";
            var ol = JsonUtility.FromJson<ObjectLevelWrapper>(json).level;
            if (ol?.objects == null) return;

            // engine coords: origin = scene CENTER, +Y down. tile space: origin bottom-left, +Y up.
            // ux = x + sceneW/2 ; uy = sceneH/2 - y   (flip Y). Toggle YFlip if it looks mirrored.
            float halfW = level.scene[0] * 0.5f;
            float halfH = level.scene[1] * 0.5f;

            var objRoot = new GameObject("Objects");
            objRoot.transform.SetParent(root.transform, false);

            GameObject playerGo = null;
            Vector3 playerPos = new Vector3(halfW, halfH, 0);

            foreach (var o in ol.objects)
            {
                float ux = o.x + halfW;
                float uy = (YFlip ? (halfH - o.y) : (o.y + halfH));

                string label = o.isPlayer ? "Player" : (o.isPortal ? "Portal" : (o.template ?? ("a" + o.typeId)));
                var go = new GameObject($"{label}_{o.x}_{o.y}");
                go.transform.SetParent(objRoot.transform, false);
                go.transform.localPosition = new Vector3(ux, uy, -1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 200 + o.layer;

                Sprite sprite = string.IsNullOrEmpty(o.img) ? null : GetSprite(o.img, o.frame);
                if (sprite != null) sr.sprite = sprite;
                else
                {
                    sr.sprite = GetMarkerSprite();
                    sr.color = o.isPlayer ? Color.cyan : (o.isPortal ? Color.green : Color.magenta);
                    go.transform.localScale = new Vector3(o.sx > 0 ? o.sx : 64, o.sy > 0 ? o.sy : 64, 1);
                }

                if (o.isPlayer)
                {
                    sr.sortingOrder = 500;
                    var rb = go.AddComponent<Rigidbody2D>();
                    rb.freezeRotation = true;
                    var cc = go.AddComponent<CapsuleCollider2D>();
                    cc.size = new Vector2(56, 92);          // ~ inside the 96px sprite
                    cc.direction = CapsuleDirection2D.Vertical;
                    cc.sharedMaterial = GetGroundMaterial(); // 0 friction -> no wall-stick

                    // all 13 a176 state anims; TurtixPlayer drives which one plays.
                    var pAnim = go.AddComponent<SpriteAnimator>();
                    foreach (var an in AnimsForTemplate(o.template))
                    {
                        var clip = BuildClip(an);
                        if (clip != null) pAnim.clips.Add(clip);
                    }
                    pAnim.playOnStart = o.template + "Stand";

                    var player = go.AddComponent<TurtixPlayer>();
                    player.anim = pAnim;
                    playerGo = go;
                    playerPos = go.transform.position;
                }
                else if (sprite != null)
                {
                    // non-player: play its default looping anim (Stand/Move) if animated.
                    string an = DefaultAnimForImageMap(o.img);
                    var clip = an != null ? BuildClip(an) : null;
                    if (clip != null && clip.frames.Length > 1)
                    {
                        var oa = go.AddComponent<SpriteAnimator>();
                        oa.clips.Add(clip);
                        oa.playOnStart = an;
                    }
                }
            }

            SetupCamera(playerGo, playerPos, level.scene[0], level.scene[1]);
        }

        // Orthographic follow-camera covering ~one screen, clamped to the level.
        private void SetupCamera(GameObject player, Vector3 playerPos, int sceneW, int sceneH)
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
            }
            var cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 384;                 // 768px tall view; bg cover-scales independent of zoom
            cam.backgroundColor = new Color(0.45f, 0.6f, 0.8f);
            camGo.transform.position = new Vector3(playerPos.x, playerPos.y, -100);

            var follow = camGo.GetComponent<CameraFollow>() ?? camGo.AddComponent<CameraFollow>();
            follow.target = player != null ? player.transform : null;
            follow.useBounds = true;
            follow.levelBounds = new Rect(0, 0, sceneW, sceneH);
        }

        private Sprite markerSprite;
        private Sprite GetMarkerSprite()
        {
            if (markerSprite != null) return markerSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            markerSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return markerSprite;
        }

        private Sprite GetSprite(string imageMapName, int frame)
        {
            string key = imageMapName + "_" + frame;
            if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            ImageMapData map = FindImageMap(imageMapName);
            if (map == null)
            {
                Debug.LogError("Unknown imageMap " + imageMapName);
                return null;
            }
            if (!File.Exists(map.png))
            {
                Debug.LogError("Missing PNG " + map.png);
                return null;
            }

            EnsureSliced(map);   // slice the sheet into named cells once per png

            // load the SPECIFIC named sub-sprite (not LoadAssetAtPath, which returns cell 0)
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(map.png)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == $"{map.name}_{frame}");

            if (sprite == null)
                Debug.LogWarning($"No sub-sprite {map.name}_{frame} in {map.png}");
            else
                spriteCache[key] = sprite;
            return sprite;
        }

        // ---- Animations ----
        private Dictionary<string, AnimData> animByName;
        private Dictionary<string, List<string>> imageMapToAnims;   // reverse: imageMap -> anim names

        private void EnsureAnimsLoaded()
        {
            if (animByName != null) return;
            animByName = new Dictionary<string, AnimData>();
            imageMapToAnims = new Dictionary<string, List<string>>();
            string p = Path.Combine(DataRoot, "animations.json");
            if (!File.Exists(p)) { Debug.LogWarning("No animations.json"); return; }
            var db = JsonUtility.FromJson<AnimDB>("{\"anims\":" + File.ReadAllText(p) + "}");
            if (db?.anims == null) return;
            foreach (var a in db.anims)
            {
                animByName[a.name] = a;
                if (!imageMapToAnims.TryGetValue(a.imageMap, out var l))
                    imageMapToAnims[a.imageMap] = l = new List<string>();
                l.Add(a.name);
            }
        }

        // Bake one engine animation into a runtime clip (resolves frame sprites).
        private SpriteAnimator.Clip BuildClip(string animName)
        {
            EnsureAnimsLoaded();
            if (!animByName.TryGetValue(animName, out var a) || a.frames == null || a.frames.Length == 0)
                return null;
            var sprites = new Sprite[a.frames.Length];
            for (int i = 0; i < a.frames.Length; i++)
                sprites[i] = GetSprite(a.imageMap, a.frames[i]);
            return new SpriteAnimator.Clip
            {
                name = animName,
                frames = sprites,
                fps = a.time > 0.001f ? a.frames.Length / a.time : 10f,
                loop = a.cycle == 1,
            };
        }

        // Default looping anim for a placed object: the animation whose imageMap == object.img.
        private string DefaultAnimForImageMap(string imageMap)
        {
            EnsureAnimsLoaded();
            if (string.IsNullOrEmpty(imageMap)) return null;
            if (imageMapToAnims.TryGetValue(imageMap, out var l) && l.Count > 0) return l[0];
            return null;
        }

        // All state anims for a template (e.g. "a176" -> a176Stand/Move/JumpUp/...).
        private List<string> AnimsForTemplate(string template)
        {
            EnsureAnimsLoaded();
            var res = new List<string>();
            if (string.IsNullOrEmpty(template)) return res;
            foreach (var name in animByName.Keys)
                if (name.Length > template.Length && name.StartsWith(template) &&
                    char.IsLetter(name[template.Length]))   // next char is a state letter, not another digit
                    res.Add(name);
            return res;
        }

        private readonly Dictionary<string, ImageMapDB> imapDbCache = new();
        private ImageMapData FindImageMap(string name)
        {
            string p = Path.Combine(DataRoot, "imagemaps.json");
            if (!imapDbCache.TryGetValue(p, out ImageMapDB db))
            {
                string json = "{\"maps\":" + File.ReadAllText(p) + "}";
                db = JsonUtility.FromJson<ImageMapDB>(json);
                imapDbCache[p] = db;
            }
            return db.maps?.FirstOrDefault(m => m.name == name);
        }

        private readonly HashSet<string> slicedPngs = new();
        private void EnsureSliced(ImageMapData map)
        {
            if (slicedPngs.Contains(map.png)) return;
            slicedPngs.Add(map.png);

            var importer = AssetImporter.GetAtPath(map.png) as TextureImporter;
            if (importer == null) { Debug.LogError("No importer for " + map.png); return; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(map.png);
            if (tex == null) { Debug.LogError("Cannot load texture " + map.png); return; }
            int cols = tex.width / map.cellW;
            int rows = tex.height / map.cellH;
            int total = cols * rows;

            // already correctly sliced? skip the (expensive) reimport.
            if (importer.spriteImportMode == SpriteImportMode.Multiple &&
                importer.spritesheet != null && importer.spritesheet.Length == total &&
                Mathf.Approximately(importer.spritePixelsPerUnit, 1f))
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 1;   // pixel-space world: 1px = 1 unit
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;

            var metas = new SpriteMetaData[total];
            int idx = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    metas[idx] = new SpriteMetaData
                    {
                        name = $"{map.name}_{idx}",
                        // frame idx counts rows top->bottom; Unity texture origin is bottom-left.
                        rect = new Rect(x * map.cellW, (rows - 1 - y) * map.cellH, map.cellW, map.cellH),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center,
                    };
                    idx++;
                }
            importer.spritesheet = metas;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
#endif
