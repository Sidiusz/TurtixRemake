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
        public class TileLevel
        {
            public string file;
            public int[] scene;
            public TileLayer[] layers;
        }

        [System.Serializable]
        public class TileLayer
        {
            public int tileW;
            public int tileH;
            public int cols;
            public int rows;
            public TileCell[] cells;
        }

        [System.Serializable]
        public class TileCell
        {
            public bool empty;
            public int frame;
            public string img;
        }

        private string[] levelNames;
        private int selectedLevel;
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

            var root = new GameObject(levelName);
            Undo.RegisterCreatedObjectUndo(root, "Import Turtix Level");

            for (int i = 0; i < level.layers.Length; i++)
            {
                var layer = level.layers[i];
                var layerGo = new GameObject($"Layer{i}");
                layerGo.transform.SetParent(root.transform, false);
                layerGo.transform.localPosition = new Vector3(0, 0, i * 0.01f);
                BuildLayer(layer, layerGo, i);
            }

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

        private void BuildLayer(TileLayer layer, GameObject parent, int layerIndex)
        {
            int cx = layer.cols;
            int cy = layer.rows;
            for (int y = 0; y < cy; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    var cell = layer.cells[y * cx + x];
                    if (cell.empty) continue;

                    Sprite sprite = GetSprite(cell.img, cell.frame);
                    if (sprite == null) continue;

                    var go = new GameObject($"{cell.img}_{cell.frame}_{x}_{y}");
                    go.transform.SetParent(parent.transform, false);

                    int uy = cy - 1 - y;
                    float px = x * Tile + Tile * 0.5f;
                    float py = uy * Tile + Tile * 0.5f;
                    go.transform.localPosition = new Vector3(px, py, 0);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = layerIndex;

                    // 96px sprites on a 128px grid: keep native pixel size, centered
                    // If cellW != Tile, Unity will draw the sprite at its native size
                    // which is correct as long as the pivot is center (default).
                    // For 64px sprites on 128 grid this leaves a gap, matching original.
                }
            }
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

            string[] sprites = AssetDatabase.FindAssets($"t:{nameof(Sprite)} {imageMapName}_{frame}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith(map.png))
                .ToArray();

            Sprite sprite = null;
            if (sprites.Length > 0)
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprites[0]);
            }
            else
            {
                sprite = SliceAndLoad(map, frame);
            }

            if (sprite != null) spriteCache[key] = sprite;
            return sprite;
        }

        private ImageMapData FindImageMap(string name)
        {
            string json = "{\"maps\":" + File.ReadAllText(Path.Combine(DataRoot, "imagemaps.json")) + "}";
            var db = JsonUtility.FromJson<ImageMapDB>(json);
            return db.maps?.FirstOrDefault(m => m.name == name);
        }

        private Sprite SliceAndLoad(ImageMapData map, int frame)
        {
            var importer = AssetImporter.GetAtPath(map.png) as TextureImporter;
            if (importer == null) return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Tile;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;

            int w = 0, h = 0;
            importer.GetWidthAndHeight(ref w, ref h);

            int cols = w / map.cellW;
            int rows = h / map.cellH;
            int total = cols * rows;
            if (frame < 0 || frame >= total)
            {
                Debug.LogError($"Frame {frame} out of range for {map.name} ({cols}x{rows})");
                return null;
            }

            var metas = new SpriteMetaData[total];
            int idx = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    metas[idx] = new SpriteMetaData
                    {
                        name = $"{map.name}_{idx}",
                        rect = new Rect(x * map.cellW, (rows - 1 - y) * map.cellH, map.cellW, map.cellH),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center,
                        border = new Vector4(0, 0, 0, 0)
                    };
                    idx++;
                }
            }
            importer.spritesheet = metas;

            AssetDatabase.ImportAsset(map.png, ImportAssetOptions.ForceUpdate);

            Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(map.png)
                .OfType<Sprite>()
                .ToArray();

            return all.FirstOrDefault(s => s.name == $"{map.name}_{frame}");
        }
    }
}
#endif
