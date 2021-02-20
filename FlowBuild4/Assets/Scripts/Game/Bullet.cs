using Photon.Realtime;
using UnityEngine;

namespace Photon.Pun.Demo.Asteroids
{
    public class Bullet : MonoBehaviour
    {
        public Player Owner { get; private set; }
        private readonly float BULLET_SPEED = 20f;

        public void Start()
        {
            Destroy(gameObject, 3.0f);
        }

        public void OnCollisionEnter(Collision collision)
        {
            Destroy(gameObject);
        }

        public void InitializeBullet(Player owner, Vector3 originalDirection, float lag)
        {
            Owner = owner;

            transform.forward = originalDirection;

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            rigidbody.velocity = originalDirection * BULLET_SPEED;
            rigidbody.position += rigidbody.velocity * lag;
        }
    }
}