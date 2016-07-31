using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Notifies the CharacterFootsteps component that the foot has collided with the ground. This will allow a sound to be played.
    /// </summary>
    public class CharacterFootTrigger : MonoBehaviour
    {
        // Internal variables
        private int m_Index;

        // Component references
        private GameObject m_Character;

        // Exposed properties
        public int Index { set { m_Index = value; } }

        /// <summary>
        /// Cache the component references and initialize default values.
        /// </summary>
        private void Awake()
        {
            var characterAnimator = GetComponentInParent<Animator>();
            m_Character = characterAnimator.gameObject;
        }

        /// <summary>
        /// The trigger has collided with another object. Send the event if the object isn't invisible or the character.
        /// </summary>
        /// <param name="other"></param>
        private void OnTriggerEnter(Collider other)
        {
            if (Utility.InLayerMask(other.gameObject.layer, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater)) {
                EventHandler.ExecuteEvent<int>(m_Character, "OnTriggerFootDown", m_Index);
            }
        }
    }
}