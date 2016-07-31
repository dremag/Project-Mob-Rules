using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The tracer will show a Line Renderer from the hitscan fire point to the hit point.
    /// </summary>
    public class Tracer : MonoBehaviour
    {
        [Tooltip("The speed that the tracer moves to the hit point")]
        [SerializeField] protected float m_Speed;

        // Internal variables
        private Vector3 m_HitPointPosition;

        // Component references
        private Transform m_Transform;
        private TrailRenderer m_TrailRenderer;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_TrailRenderer = GetComponent<TrailRenderer>();
        }

        /// <summary>
        /// Enables the TrailRenderer and schedules the projectile's activation if it isn't activated beforehand.
        /// </summary>
        private void OnEnable()
        {
            // Reset the TrailRenderer time if is a negative value. This is done to prevent the trail from being rendered when the object pool changes the position of the projectile.
            if (m_TrailRenderer && m_TrailRenderer.time < 0) {
                Scheduler.Schedule(0.001f, ResetTrails);
            }
        }

        /// <summary>
        /// When the TrailRenderer is pooled the trail can still be seen when it is switching positions. At this point the time has been set to a negative value and has waited
        /// a frame. By doing this the trail will not render when switching positions.
        /// </summary>
        private void ResetTrails()
        {
            m_TrailRenderer.time = -m_TrailRenderer.time;
        }

        /// <summary>
        /// Cancel the scheduled activation if the timer isn't what caused the projectile to deactivate, and disable the TrailRenderer.
        /// </summary>
        private void OnDisable()
        {
            // Set the TrailRenderer time to a negative value to prevent a trail from being added when the object pool changes the position of the projectile.
            if (m_TrailRenderer) {
                m_TrailRenderer.time = -m_TrailRenderer.time;
            }
        }

        /// <summary>
        /// Moves to the hit point position.
        /// </summary>
        private void Update()
        {
            m_Transform.position = Vector3.MoveTowards(m_Transform.position, m_HitPointPosition, m_Speed * Time.deltaTime);

            // Return to the object pool after arriving at the hit point.
            if ((m_Transform.position - m_HitPointPosition).sqrMagnitude < 0.1f) {
                ObjectPool.Return(gameObject);
            }
        }

        /// <summary>
        /// Sets the hit point that the tracer should move to.
        /// </summary>
        /// <param name="hitPoint">The hit point position</param>
        public void SetHitPoint(Vector3 hitPoint)
        {
            m_HitPointPosition = hitPoint;
        }
    }
}