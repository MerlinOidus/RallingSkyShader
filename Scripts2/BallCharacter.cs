using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A character in place of the ball: the rigged model stands where the ball is, faces down the track and drives
    /// its Animator from the ball. Animator parameters (each optional): Speed (float, rows per second, 0 before the
    /// tap and after death), Grounded (bool), Jump (trigger, when the ball takes off), Crash (trigger, when it dies).
    /// RTX ON > Build Character Animator makes a controller with Idle, Run, Jump and Crash from your clips.
    /// The ball itself still decides everything (steering, jumps, hits); the character is only its look.
    /// </summary>
    [RequireComponent(typeof(BallController))]
    public class BallCharacter : MonoBehaviour
    {
        [Tooltip("The rigged model (its FBX or a prefab), e.g. CuteBeard. Empty = the ball as usual.")]
        public GameObject model;
        [Tooltip("Animator Controller with Idle, Run, Jump, Crash (RTX ON > Build Character Animator). Empty = the model's own.")]
        public RuntimeAnimatorController controller;
        [Tooltip("Height of the character in tiles (1 = one tile wide).")]
        public float height = 0.9f;
        [Tooltip("Turn about the up axis in degrees, if the model does not face down the track.")]
        public float yaw;
        [Tooltip("Extra shift after fitting (x = sideways, y = up, z = forward), in tiles.")]
        public Vector3 offset;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int CrashId = Animator.StringToHash("Crash");

        BallController ball;
        Animator animator;
        bool hasSpeed, hasGrounded, hasJump, hasCrash;
        int jumps;
        bool wasAlive = true;

        void Start()
        {
            ball = GetComponent<BallController>();
            if (model == null) return;

            var holder = new GameObject("Character").transform;
            holder.SetParent(transform, false);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var v = Instantiate(model, holder, false);
            v.name = model.name;
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            foreach (var c in v.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // Same layer as the ball's sphere, so the ball's own reflection probe does not see it.
            int layer = ball.visual != null ? ball.visual.gameObject.layer : gameObject.layer;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            // Fit: `height` tall, feet on the floor under the ball, centred on it.
            if (WorldBounds(v, out var b) && b.size.y > 0.001f) holder.localScale = Vector3.one * (height / b.size.y);
            if (WorldBounds(v, out b))
            {
                var p = transform.position;
                holder.position += new Vector3(p.x - b.center.x, p.y - ball.radius - b.min.y, p.z - b.center.z);
            }
            holder.localPosition += offset;

            animator = v.GetComponentInChildren<Animator>();
            if (animator == null) animator = v.AddComponent<Animator>();
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the ball moves it; clips stay in place
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var prm in animator.parameters)
            {
                if (prm.nameHash == SpeedId && prm.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                else if (prm.nameHash == GroundedId && prm.type == AnimatorControllerParameterType.Bool) hasGrounded = true;
                else if (prm.nameHash == JumpId && prm.type == AnimatorControllerParameterType.Trigger) hasJump = true;
                else if (prm.nameHash == CrashId && prm.type == AnimatorControllerParameterType.Trigger) hasCrash = true;
            }

            ball.UseCharacter(holder);
        }

        static bool WorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        void LateUpdate()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            bool running = ball.Alive && ball.Moving;
            if (hasSpeed) animator.SetFloat(SpeedId, running ? ball.Speed : 0f);
            if (hasGrounded) animator.SetBool(GroundedId, ball.Grounded || !running);
            if (hasJump && ball.Jumps != jumps) animator.SetTrigger(JumpId);
            if (hasCrash && !ball.Alive && wasAlive) animator.SetTrigger(CrashId);
            jumps = ball.Jumps;
            wasAlive = ball.Alive;
        }
    }
}
