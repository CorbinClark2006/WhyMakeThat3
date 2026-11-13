using UnityEngine;

namespace Convoy
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ConvoySpriteAvatar : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public Sprite idleFront, idleBack;
        public Sprite walk1Front, walk2Front, walk1Back, walk2Back;
        public Sprite jumpFront, jumpBack;
        public const float WalkFrameDuration = 0.14f;

        private bool facingBack = true;
        private bool walking;
        private float walkTime;

        private void Start()
        {
            if (spriteRenderer == null || idleFront == null || idleBack == null ||
                walk1Front == null || walk2Front == null || walk1Back == null || walk2Back == null ||
                jumpFront == null || jumpBack == null)
            {
                Debug.LogError("Player sprite avatar is missing its renderer or animation frames.", this);
                enabled = false;
            }
        }

        public void Animate(Vector2 movement, bool grounded, float dt)
        {
            bool previousFacing = facingBack;
            if (Mathf.Abs(movement.y) > 0.01f)
                facingBack = movement.y > 0f;
            bool moving = grounded && movement.sqrMagnitude > 0.01f;
            if (!moving || !walking || previousFacing != facingBack)
                walkTime = 0f;
            else
                walkTime = (walkTime + dt) % (WalkFrameDuration * 2f);
            walking = moving;

            if (!grounded)
                spriteRenderer.sprite = facingBack ? jumpBack : jumpFront;
            else if (!moving)
                spriteRenderer.sprite = facingBack ? idleBack : idleFront;
            else if (walkTime < WalkFrameDuration)
                spriteRenderer.sprite = facingBack ? walk1Back : walk1Front;
            else
                spriteRenderer.sprite = facingBack ? walk2Back : walk2Front;
        }

        public void FaceCamera(Camera view, Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, up);
            if (forward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(forward, up);
        }
    }
}
