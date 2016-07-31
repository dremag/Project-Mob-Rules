using UnityEngine;
using System.Collections;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A shell spawns when a weapon is fired representing the bullet's shell.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class Shell : MonoBehaviour
    {
        [Tooltip("The length of time that the shell should exist for")]
        [SerializeField] protected float m_Lifespan = 5;

        // Component references
        private GameObject m_GameObject;
        private CapsuleCollider m_CapsuleCollder;
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_CapsuleCollder = GetComponent<CapsuleCollider>();
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// A weapon has been fired and the shell has spawned. Disable the CapsuleCollider for a short amount of time to prevent two shells from sticking to each other.
        /// </summary>
        private void OnEnable()
        {
            m_CapsuleCollder.enabled = false;
            m_Rigidbody.isKinematic = false;

            // Wait a short amount of time for the shell to clear the chamber before activating the collider. If the collider is enabled initially
            // then two shells could be instantiated at the same position and they could get stuck on each other.
            Scheduler.Schedule(0.2f, EnableCollider);
        }

        /// <summary>
        /// The shell should be clear of any other objects so enable the collider to allow it to hit the ground properly.
        /// </summary>
        private void EnableCollider()
        {
            m_CapsuleCollder.enabled = true;
            Scheduler.Schedule(m_Lifespan, Destroy);
        }

        /// <summary>
        /// Physics get weird when things are very small. Set the shell to being kinematic when it enters a collision to prevent it from bouncing around.
        /// </summary>
        /// <param name="col">The entered collision.</param>
        private void OnCollisionEnter(Collision col)
        {
            if (col.gameObject.GetComponent<Shell>() == null) {
                m_Rigidbody.isKinematic = true;
            }
        }

        /// <summary>
        /// Place itself back in the ObjectPool.
        /// </summary>
        private void Destroy()
        {
            ObjectPool.Destroy(m_GameObject);
        }
    }
}