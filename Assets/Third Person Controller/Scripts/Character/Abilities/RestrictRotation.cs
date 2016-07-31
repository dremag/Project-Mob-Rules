using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Restrict Rotation ability will restrict the character to a specified rotation interval.
    /// </summary>
    public class RestrictRotation : Ability
    {
        [Tooltip("The amount of degrees that the character can rotate between")]
        [SerializeField] protected float m_Restriction = 180f;
        [Tooltip("Any offset that should be applied to the initial look rotation")]
        [SerializeField] protected float m_StartOffset = 90f;
        [Tooltip("Any offset that should be applied to the final, restricted rotation")]
        [SerializeField] protected float m_EndOffset = 90f;

        /// <summary>
        /// Can this ability run at the same time as another ability?
        /// </summary>
        /// <returns>True if this ability can run with another ability.</returns>
        public override bool IsConcurrentAblity()
        {
            return true;
        }

        public override bool UpdateRotation()
        {
            // Force the y angle to be within 0 - 360.
            var angle = Utility.RestrictAngle(m_Controller.LookRotation.eulerAngles.y + m_StartOffset);
            if (angle < 0) {
                angle += 360;
            }

            // Do not update the rotation if the character is looking in the move direction and the move direction is an invalid direction.
            if (m_Controller.LookInMoveDirection && angle % m_Restriction != 0) {
                return false;
            }

            // Find the closest angle to the degree restriction.
            angle = Mathf.Round(angle / m_Restriction) * m_Restriction;

            // Set the restricted rotation.
            var rotation = m_Transform.eulerAngles;
            rotation.x = rotation.z = 0;
            rotation.y = angle + m_Restriction + m_EndOffset;

            // Rotate towards the restricted angle.
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.Euler(rotation), m_Controller.RotationSpeed * Time.fixedDeltaTime);

            return false;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if the Animator should not update to reflect the current state.</returns>
        public override bool HasAnimatorControl()
        {
            return false;
        }
    }
}