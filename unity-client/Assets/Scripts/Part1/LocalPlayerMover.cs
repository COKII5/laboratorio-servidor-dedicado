using UnityEngine;

namespace Lab.Part1
{
    /// <summary>Mueve al jugador local sobre el plano XZ con WASD o las flechas.</summary>
    public class LocalPlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f)] float speed = 5f;

        void Update()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            transform.position += Vector3.ClampMagnitude(input, 1f) * (speed * Time.deltaTime);
        }
    }
}
