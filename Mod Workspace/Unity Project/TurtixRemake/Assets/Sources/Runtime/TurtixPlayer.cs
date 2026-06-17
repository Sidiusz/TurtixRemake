using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtix.Unity
{
    /// Network-agnostic 2D platformer controller (pixel-space: 1 unit = 1 px).
    /// Uses the new Input System (Keyboard.current). Reads input only when controlEnabled
    /// (single-player: always; coop: set = IsOwner). Movement brain stays the same online/offline.
    [RequireComponent(typeof(Rigidbody2D))]
    public class TurtixPlayer : MonoBehaviour
    {
        [Header("Control")]
        public bool controlEnabled = true;

        [Header("Tuning (pixel units)")]
        public float moveSpeed = 360f;     // px/s
        public float jumpSpeed = 1050f;    // px/s initial jump velocity
        public float gravity = 2600f;      // px/s^2 (applied via gravityScale)
        public int maxJumps = 2;           // double jump (Turtix has a176DoubleJump*)

        [Header("Ground check")]
        public float groundCheckDist = 8f;
        public LayerMask groundMask = ~0;

        [Header("Animation")]
        public SpriteAnimator anim;     // a176 state clips; assigned by importer
        public string animPrefix = "a176";

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer sr;
        private int jumpsLeft;
        private bool jumpQueued;
        private float moveInput;
        private bool grounded;
        private bool usedDoubleJump;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            float g = Mathf.Abs(Physics2D.gravity.y);
            rb.gravityScale = gravity / (g > 0.001f ? g : 9.81f);
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Update()
        {
            if (!controlEnabled) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            float right = (kb.rightArrowKey.isPressed || kb.dKey.isPressed) ? 1f : 0f;
            float left = (kb.leftArrowKey.isPressed || kb.aKey.isPressed) ? 1f : 0f;
            moveInput = right - left;

            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                jumpQueued = true;

            if (sr != null && Mathf.Abs(moveInput) > 0.01f)
                sr.flipX = moveInput < 0f;

            UpdateAnim();
        }

        void UpdateAnim()
        {
            if (anim == null) return;
            float vy = rb.linearVelocity.y;
            string state;
            if (!grounded)
            {
                bool dbl = usedDoubleJump;
                state = vy > 0.01f ? (dbl ? "DoubleJumpUp" : "JumpUp")
                                   : (dbl ? "DoubleJumpDown" : "JumpDown");
            }
            else
            {
                state = Mathf.Abs(moveInput) > 0.01f ? "Move" : "Stand";
            }
            anim.Play(animPrefix + state);
        }

        void FixedUpdate()
        {
            if (!controlEnabled) return;

            var v = rb.linearVelocity;
            v.x = moveInput * moveSpeed;

            grounded = IsGrounded();
            if (grounded && v.y <= 0.01f) { jumpsLeft = maxJumps; usedDoubleJump = false; }

            if (jumpQueued)
            {
                if (jumpsLeft > 0)
                {
                    v.y = jumpSpeed;
                    jumpsLeft--;
                    if (jumpsLeft < maxJumps - 1) usedDoubleJump = true;  // 2nd+ jump = double
                }
                jumpQueued = false;
            }
            rb.linearVelocity = v;
        }

        bool IsGrounded()
        {
            if (col == null) return false;
            Bounds b = col.bounds;
            Vector2 origin = new Vector2(b.center.x, b.min.y + 1f);
            Vector2 size = new Vector2(b.size.x * 0.9f, 2f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDist, groundMask);
            return hit.collider != null && hit.collider != col;
        }
    }
}
