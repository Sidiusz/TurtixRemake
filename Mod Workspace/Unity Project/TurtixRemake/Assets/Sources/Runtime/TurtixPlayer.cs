using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtix.Unity
{
    /// Network-agnostic 2D platformer controller (pixel-space: 1 unit = 1 px).
    /// Manual gravity + raycast ground detection so it behaves the same on flat ground and
    /// slopes: movement is projected along the slope so the player never slides when idle and
    /// never needs a friction material. Reads input only when controlEnabled (coop: = IsOwner).
    [RequireComponent(typeof(Rigidbody2D))]
    public class TurtixPlayer : MonoBehaviour
    {
        [Header("Control")]
        public bool controlEnabled = true;

        [Header("Tuning (pixel units)")]
        public float moveSpeed = 360f;     // px/s
        public float jumpSpeed = 1050f;    // px/s initial jump velocity
        public float gravity = 2600f;      // px/s^2 (manual)
        public int maxJumps = 2;           // double jump

        [Header("Ground check")]
        public LayerMask groundMask = ~0;
        public float groundProbe = 10f;       // ray length below the feet
        public float maxSlope = 60f;          // deg considered walkable ground
        public float coyoteTime = 0.08f;
        public float jumpBufferTime = 0.1f;

        [Header("Animation")]
        public SpriteAnimator anim;
        public string animPrefix = "a176";

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer sr;
        private float moveInput;
        private int jumpsLeft;
        private float coyote;
        private float jumpBuffer;
        private bool grounded;
        private Vector2 groundNormal = Vector2.up;
        private bool usedDoubleJump;
        private bool facingRight;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            rb.gravityScale = 0f;                 // we apply gravity manually
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Update()
        {
            if (!controlEnabled) { moveInput = 0f; return; }
            var kb = Keyboard.current;
            if (kb == null) return;

            float right = (kb.rightArrowKey.isPressed || kb.dKey.isPressed) ? 1f : 0f;
            float left = (kb.leftArrowKey.isPressed || kb.aKey.isPressed) ? 1f : 0f;
            moveInput = right - left;

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                jumpBuffer = jumpBufferTime;

            // Turtix base art faces LEFT -> mirror when moving right.
            if (Mathf.Abs(moveInput) > 0.01f) facingRight = moveInput > 0f;
            if (sr != null) sr.flipX = facingRight;

            UpdateAnim();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ProbeGround();

            Vector2 v = rb.linearVelocity;

            if (grounded)
            {
                jumpsLeft = maxJumps;
                usedDoubleJump = false;
                coyote = coyoteTime;

                // move along the slope so walking up/down hills is smooth & idle = no slide.
                float speed = moveInput * moveSpeed;
                v.x = speed;
                v.y = (Mathf.Abs(speed) > 0.01f) ? speed * (-groundNormal.x / Mathf.Max(groundNormal.y, 0.001f)) : 0f;
            }
            else
            {
                coyote -= dt;
                v.x = moveInput * moveSpeed;          // air control
                v.y -= gravity * dt;                  // manual gravity
            }

            if (jumpBuffer > 0f)
            {
                bool groundJump = coyote > 0f && jumpsLeft == maxJumps;
                if (groundJump || jumpsLeft > 0)
                {
                    v.y = jumpSpeed;
                    jumpsLeft--;
                    if (!groundJump) usedDoubleJump = true;
                    grounded = false;
                    coyote = 0f;
                    jumpBuffer = 0f;
                }
            }
            jumpBuffer -= dt;

            rb.linearVelocity = v;
        }

        void ProbeGround()
        {
            grounded = false;
            groundNormal = Vector2.up;
            if (col == null) return;
            Bounds b = col.bounds;
            float y = b.min.y + 2f;
            float[] xs = { b.min.x + 3f, b.center.x, b.max.x - 3f };
            float bestY = float.NegativeInfinity;
            foreach (float x in xs)
            {
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, y), Vector2.down, groundProbe, groundMask);
                if (hit.collider == null || hit.collider == col || hit.collider.isTrigger) continue;
                if (rb.linearVelocity.y > 0.5f) continue;             // moving up = not landing
                if (Vector2.Angle(hit.normal, Vector2.up) > maxSlope) continue;
                grounded = true;
                if (hit.point.y > bestY) { bestY = hit.point.y; groundNormal = hit.normal; }
            }
        }

        void UpdateAnim()
        {
            if (anim == null) return;
            string state;
            if (!grounded)
                state = rb.linearVelocity.y > 0.01f ? (usedDoubleJump ? "DoubleJumpUp" : "JumpUp")
                                                    : (usedDoubleJump ? "DoubleJumpDown" : "JumpDown");
            else
                state = Mathf.Abs(moveInput) > 0.01f ? "Move" : "Stand";
            anim.Play(animPrefix + state);
        }
    }
}
