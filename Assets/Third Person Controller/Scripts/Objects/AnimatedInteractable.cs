using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Play an animation when the interactor interacts with the AnimatedInteractable.
    /// </summary>
    public class AnimatedInteractable : MonoBehaviour, IInteractableTarget
    {
        [Tooltip("The name of the state to play when the object is interacted with ")]
        [SerializeField] protected string m_InteractionName;

        // Internal variables
        private int m_InteractionStateHash;

        // Component references
        private Animator m_Animator;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected virtual void Awake()
        {
            m_Animator = GetComponent<Animator>();

            m_InteractionStateHash = Animator.StringToHash(m_InteractionName);
        }

        /// <summary>
        /// Can the object be interacted with?
        /// </summary>
        /// <returns>True if the object can be interacted with.</returns>
        public virtual bool IsInteractionReady()
        {
#if UNITY_4_6 || UNITY_4_7
            return !m_Animator.GetCurrentAnimatorStateInfo(0).nameHash.Equals(m_InteractionStateHash);
#else
            return !m_Animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(m_InteractionStateHash);
#endif
        }

        /// <summary>
        /// Play the interaction animation.
        /// </summary>
        public virtual void Interact()
        {
            m_Animator.Play(m_InteractionStateHash);
        }
    }
}