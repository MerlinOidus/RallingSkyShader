using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Kinematic ball: rolls forward at a fixed speed (rows per second), steers by dragging,
    /// stands on any non-trigger collider below it and dies on anything with a Hazard.
    /// No Rigidbody, so the motion is exact and the rhythm of the level is stable.
    /// </summary>
    public class BallController : MonoBehaviour
    {
        public float radius = 0.3f;
        [Tooltip("How close the ball's centre must get to a deadly box to die. The original only checks which tile the " +
                 "ball's centre is on, so this is small; raise it to make hazards stricter.")]
        public float hazardHitRadius = 0.05f;
        [Tooltip("World units moved per full screen-width drag.")]
        public float dragSensitivity = 6.5f;
        public float keySteerSpeed = 7f;
        [Tooltip("Controller: world units per second with the left stick, LT/RT or D-pad pushed all the way.")]
        public float stickSteerSpeed = 9f;
        [Tooltip("How fast the ball glides to where it is steered (Settings > Smooth on). Smooth off: at once.")]
        public float steerSharpness = 30f;
        [Tooltip("How far the ball can steer from the middle, in lanes: 3 = 7 lanes wide, the 5 lanes of the track plus " +
                 "one lane of empty space on each side, where the ball falls off.")]
        public float lateralLimit = 3f;
        public float gravity = 35f;
        public float killY = -6f;
        [Tooltip("How far (in rows) the ball may roll past the edge of the floor before it is falling for good. Small, " +
                 "so a hole of one row always swallows it, at any speed, like the original (it checks the tile under " +
                 "the ball's centre); the seams between tiles are much smaller than this.")]
        public float edgeGrace = 0.3f;
        public Transform visual;

        public float Speed { get; private set; }
        public bool Grounded { get; private set; }
        /// <summary>Flying from a jump pad; some hazards cannot hurt the ball then (Hazard.jumpSafe).</summary>
        public bool Jumping { get; private set; }
        /// <summary>How many times the ball took off (jump pads, bounce hops); a new jump = this went up.</summary>
        public int Jumps { get; private set; }
        public bool Alive { get; private set; } = true;
        /// <summary>Rolling down the track (after the tap, until death or the finish).</summary>
        public bool Moving => moving;
        public Vector3 Position => transform.position;

        float targetX;
        float vy;
        float unsupported; // distance rolled with no floor under the ball (not jumping)
        bool falling;      // rolled off the floor: it does not come back up onto the next tile
        float activeGravity;
        bool moving;
        bool dragging;
        float lastPointerX;
        bool smoothSteer = true;
        bool cursorLocked, skipMouseDelta; // Settings > Auto Move: the hidden, locked cursor steers
        readonly Collider[] overlap = new Collider[16];

        // Bounce power-up: hop after hop, each landing exactly on its row.
        System.Collections.Generic.List<float> hops;
        int hopIndex;
        float hopTargetZ, bounceEndZ, bounceHeight;
        public bool Bouncing => hops != null;

        // Invisible power-up.
        float hiddenUntil = -1f;
        Renderer[] visualRenderers;

        // A character stands in for the ball (BallCharacter): it does not roll or shatter.
        bool characterVisual;

        /// <summary>Puts <paramref name="character"/> in place of the ball's sphere (hidden from now on).</summary>
        public void UseCharacter(Transform character)
        {
            if (visual != null && visual != character) visual.gameObject.SetActive(false);
            visual = character;
            visualRenderers = null;
            characterVisual = true;
        }

        void Awake()
        {
            activeGravity = gravity;
            targetX = transform.position.x;
        }

        public void Begin(float rowsPerSecond)
        {
            Speed = rowsPerSecond;
            moving = true;
        }

        public void Stop() => moving = false;

        /// <summary>Moves the ball (before play starts) and keeps the steering target in the same lane.</summary>
        public void PlaceAt(Vector3 position)
        {
            transform.position = position;
            targetX = position.x;
            unsupported = 0f;
            falling = false;
        }

        /// <summary>Moves the ball along the track by <paramref name="rows"/> (GameManager keeps it on the music).</summary>
        public void Nudge(float rows)
        {
            if (!moving || !Alive) return;
            transform.position += new Vector3(0f, 0f, rows);
        }

        /// <summary>Changes forward speed (rows per second), e.g. for levels that speed up.</summary>
        public void SetSpeed(float rowsPerSecond) => Speed = rowsPerSecond;

        // Longest physics step. A frame hitch (scene just loaded, a song loading) can last 0.3 s; moved in one step,
        // gravity would carry the ball below the floor before the ground check sees it, and it falls through.
        const float MaxStep = 1f / 60f;

        void Update()
        {
            UpdateCursor();
            if (!moving || !Alive) return;
            float frame = Time.deltaTime;
            smoothSteer = GameSettings.SmoothSteer;
            Steer(frame);

            // Same distance as one big step (the ball stays on the music), in small steps.
            int steps = Mathf.Clamp(Mathf.CeilToInt(frame / MaxStep), 1, 30);
            float dt = frame / steps;
            for (int i = 0; i < steps && moving && Alive; i++) Step(dt);
        }

        void Step(float dt)
        {
            Vector3 p = transform.position;
            Vector3 before = p;
            p.z += Speed * dt;
            p.x = smoothSteer ? Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-steerSharpness * dt)) : targetX;

            vy -= activeGravity * dt;
            p.y += vy * dt;

            Physics.SyncTransforms();
            Grounded = false;
            if (!falling && vy <= 0f && Physics.SphereCast(new Vector3(p.x, p.y + 1f, p.z), 0.1f, Vector3.down,
                    out var hit, 1f + radius + 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                float bottom = p.y - radius;
                float tolerance = Mathf.Max(0.45f, -vy * dt + 0.1f);
                // Snap only when we are at the surface, not when already sinking past its edge.
                if (bottom <= hit.point.y + 0.05f && bottom >= hit.point.y - tolerance)
                {
                    p.y = hit.point.y + radius;
                    vy = 0f;
                    activeGravity = gravity;
                    Grounded = true;
                    Jumping = false;
                    bool padHere = false;

                    // Jump as soon as the ball rolls onto the pad (like the original), still landing exactly
                    // pad.rows rows past the pad's centre. Jumping from the centre was too late: the arc was
                    // only 0.8 high at the front of a riser right after the pad, so the ball hit it.
                    var pad = hit.collider.GetComponentInParent<JumpPad>();
                    float padZ = pad != null ? pad.transform.position.z : 0f;
                    if (pad != null && p.z >= padZ - 0.45f)
                    {
                        hops = null; // a jump pad ends a bounce
                        Launch(pad.rows + padZ - p.z, pad.height);
                        pad.Pop();
                        padHere = true;
                    }
                    if (!padHere && hops != null) NextHop(p.z);
                }
            }

            // Rolled off the floor (a hole, the side of the track): past edgeGrace the ball is falling and is not
            // snapped back up onto the next tile. Before, a one-row hole was crossed: the ball only sank a little
            // over it and the snap tolerance put it back on the far tile.
            if (Grounded) unsupported = 0f;
            else if (!Jumping && !falling)
            {
                unsupported += new Vector2(p.x - before.x, p.z - before.z).magnitude;
                if (unsupported > edgeGrace) falling = true;
            }

            transform.position = p;

            if (visual != null && !characterVisual)
                visual.Rotate(Vector3.right, Speed * dt / radius * Mathf.Rad2Deg, Space.World);

            CheckContacts(p);

            if (p.y < killY) GameManager.Instance.Die();
        }

        /// <summary>Auto Move: the cursor is locked and hidden while the ball rolls, and comes back on pause, crash and finish.</summary>
        void UpdateCursor()
        {
            bool want = moving && Alive && Time.timeScale > 0f && GameSettings.AutoMove && GameInput.HasMouse;
            if (want == cursorLocked) return;
            cursorLocked = want;
            skipMouseDelta = want;
            Cursor.lockState = want ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !want;
        }

        void OnDisable()
        {
            if (!cursorLocked) return;
            cursorLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Steer(float dt)
        {
            if (Time.timeScale == 0f) // paused: clicks on the Pause menu do not steer
            {
                dragging = false;
                return;
            }
            if (cursorLocked)
            {
                // Auto Move: the mouse steers without a button (the first frame after locking holds the jump to the middle).
                if (!skipMouseDelta)
                    targetX += GameInput.MouseDeltaX() / Mathf.Max(1, Screen.width) * dragSensitivity * GameSettings.DragSensitivity;
                skipMouseDelta = false;
                dragging = false;
            }
            else if (GameInput.PointerHeld(out float px))
            {
                if (dragging)
                    targetX += (px - lastPointerX) / Mathf.Max(1, Screen.width) * dragSensitivity * GameSettings.DragSensitivity;
                dragging = true;
                lastPointerX = px;
            }
            else dragging = false;

            // Accelerator (A / Cross or Space held): keys and controller steer faster (Settings > Accelerator).
            float boost = GameInput.AcceleratorHeld() ? GameSettings.Accelerator : 1f;
            targetX += GameInput.KeyAxis() * keySteerSpeed * boost * dt;
            targetX += GameInput.StickAxis() * stickSteerSpeed * GameSettings.StickSensitivity * boost * dt;
            // X / B: exactly one lane left or right, to the middle of the lane next to the one the ball is heading for.
            int step = GameInput.LaneStep();
            if (step != 0) targetX = Mathf.Round(targetX) + step;
            targetX = Mathf.Clamp(targetX, -lateralLimit, lateralLimit);
        }

        /// <summary>
        /// Bounce power-up: hops of <see cref="PartArgs.Hops"/> rows (repeated) from <paramref name="fromZ"/> until
        /// Distance rows are covered. Each landing needs floor; without it the ball falls.
        /// </summary>
        public void StartBounce(float fromZ, PartArgs args)
        {
            var list = args.Hops();
            if (list.Count == 0 || args.distance <= 0f) return;
            hops = list;
            hopIndex = 0;
            bounceEndZ = fromZ + args.distance - 0.01f;
            bounceHeight = Mathf.Max(0.2f, args.height);
            hopTargetZ = fromZ;
            NextHop(transform.position.z);
        }

        /// <summary>Landed during a bounce: the next hop, or the end of the bounce.</summary>
        void NextHop(float z)
        {
            if (hopTargetZ >= bounceEndZ)
            {
                hops = null;
                return;
            }
            float hop = hops[hopIndex++ % hops.Count];
            hopTargetZ += hop;
            Launch(Mathf.Max(0.3f, hopTargetZ - z), bounceHeight * hop / 4f);
        }

        /// <summary>Invisible power-up: the ball disappears for <paramref name="seconds"/> (it still plays as normal).</summary>
        public void Hide(float seconds)
        {
            hiddenUntil = Time.time + Mathf.Max(0.1f, seconds);
            ShowVisual(false);
        }

        void ShowVisual(bool on)
        {
            if (visual == null) return;
            if (visualRenderers == null) visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in visualRenderers) if (r != null) r.enabled = on;
        }

        void LateUpdate()
        {
            if (hiddenUntil < 0f || !Alive) return;
            float left = hiddenUntil - Time.time;
            if (left <= 0f)
            {
                hiddenUntil = -1f;
                ShowVisual(true);
            }
            else if (left < 0.5f) ShowVisual(Mathf.Repeat(left, 0.12f) < 0.06f); // blinks just before it comes back
        }

        /// <summary>Jump that lands exactly <paramref name="rows"/> rows ahead.</summary>
        public void Launch(float rows, float height)
        {
            unsupported = 0f;
            falling = false;
            float t = rows / Mathf.Max(0.01f, Speed);
            activeGravity = 8f * height / (t * t);
            vy = 4f * height / t;
            Grounded = false;
            Jumping = true;
            Jumps++;
            GameAudio.Play(Sfx.Jump);
        }

        void CheckContacts(Vector3 p)
        {
            int n = Physics.OverlapSphereNonAlloc(p, radius * 0.85f, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c == null || !c.isTrigger) continue;

                var gem = c.GetComponentInParent<Gem>();
                if (gem != null) { gem.Collect(); continue; }
                var crown = c.GetComponentInParent<CrownPickup>();
                if (crown != null) { crown.Collect(); continue; }
                var power = c.GetComponentInParent<PowerUp>();
                if (power != null) { power.Collect(this); continue; }
                var custom = c.GetComponentInParent<CustomPickup>();
                if (custom != null) { custom.Collect(); continue; }

                var hz = c.GetComponentInParent<Hazard>();
                if (hz != null && hz.lethal && !(hz.jumpSafe && Jumping) && !GameManager.Instance.Invincible &&
                    (c.ClosestPoint(p) - p).sqrMagnitude <= hazardHitRadius * hazardHitRadius)
                {
                    GameManager.Instance.Die();
                    return;
                }
            }
        }

        public void Kill(Material shardMaterial)
        {
            if (!Alive) return;
            Alive = false;
            moving = false;
            if (characterVisual) return; // the character plays its Crash animation instead
            if (visual != null) visual.gameObject.SetActive(false);

            // Shatter into little beveled shards.
            var mesh = MeshFactory.BeveledBox(Vector3.one * 0.14f, 0.03f);
            var shards = new Collider[16];
            for (int i = 0; i < shards.Length; i++)
            {
                var shard = new GameObject("Shard");
                shard.transform.position = transform.position + Random.insideUnitSphere * radius * 0.6f;
                shard.transform.rotation = Random.rotation;
                shard.AddComponent<MeshFilter>().sharedMesh = mesh;
                shard.AddComponent<MeshRenderer>().sharedMaterial = shardMaterial;
                var box = shard.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.14f;
                // Shards start inside each other: pushing apart would fire them off at huge speed (a white flash
                // when one flies past the camera), so they only bump into the track.
                for (int j = 0; j < i; j++) Physics.IgnoreCollision(box, shards[j]);
                shards[i] = box;
                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.maxDepenetrationVelocity = 1f;
                rb.AddForce((Random.onUnitSphere + Vector3.up * 1.2f + Vector3.forward * 0.8f) * 0.25f, ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * 0.02f, ForceMode.Impulse);
                Destroy(shard, 4f);
            }
        }
    }
}
