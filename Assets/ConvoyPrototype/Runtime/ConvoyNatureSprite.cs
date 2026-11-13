using UnityEngine;

namespace Convoy
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ConvoyNatureSprite : MonoBehaviour
    {
        private void OnWillRenderObject()
        {
            Camera camera = Camera.current;
            if (camera == null)
                return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.position - camera.transform.position, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
