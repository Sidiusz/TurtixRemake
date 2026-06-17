using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtix.Unity
{
    /// Frame-cycling sprite animator — 1:1 with the engine t2dAnimationDatablock model
    /// (a flat frame-index list played over a fixed total time, optionally looping).
    /// Deterministic (no Unity Animator), so it stays in sync for co-op.
    /// Clips are baked by the importer; play by name.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 10f;      // frames.Length / animationTime
            public bool loop = true;     // animationCycle
        }

        public List<Clip> clips = new List<Clip>();
        public string playOnStart = "";

        private SpriteRenderer sr;
        private Clip current;
        private float t;
        private int frame;

        public bool IsFinished { get; private set; }
        public string CurrentName => current != null ? current.name : "";

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void Start()
        {
            if (!string.IsNullOrEmpty(playOnStart)) Play(playOnStart);
            else if (clips.Count > 0) Play(clips[0].name);
        }

        public bool Has(string name) => Find(name) != null;

        /// Play a clip by name. No-op if already playing it (unless restart=true).
        public void Play(string name, bool restart = false)
        {
            if (current != null && current.name == name && !restart) return;
            Clip c = Find(name);
            if (c == null || c.frames == null || c.frames.Length == 0) return;
            current = c; t = 0f; frame = 0; IsFinished = false;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            sr.sprite = c.frames[0];
        }

        void Update()
        {
            if (current == null || current.frames.Length == 0) return;
            float fps = current.fps > 0.001f ? current.fps : 10f;
            t += Time.deltaTime * fps;
            int n = current.frames.Length;
            int idx = Mathf.FloorToInt(t);

            if (current.loop) idx %= n;
            else if (idx >= n) { idx = n - 1; IsFinished = true; }

            if (idx != frame)
            {
                frame = idx;
                sr.sprite = current.frames[frame];
            }
        }

        private Clip Find(string name)
        {
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].name == name) return clips[i];
            return null;
        }
    }
}
