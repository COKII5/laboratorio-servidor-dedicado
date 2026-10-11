using UnityEngine;

namespace Lab.Common
{
    public class LocalPlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] float speed = 5f;

        public float SpeedMultiplier { get; set; } = 1f;

        void Update()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            transform.position += Vector3.ClampMagnitude(input, 1f) * (speed * SpeedMultiplier * Time.deltaTime);
        }
    }
}
