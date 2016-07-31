using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Plays a random footstep sound when the foot touches the ground. This is sent through an animation event.
    /// </summary>
    public class CharacterFootsteps : MonoBehaviour
    {
        /// <summary>
        /// Stores the footstep sound along with what foot that sound maps to. The foot specification is only used if PerFootSounds is enabled.
        /// </summary>
        [System.Serializable]
        protected class FootstepSound
        {
            [Tooltip("The sound to play")]
            [SerializeField] protected AudioClip m_Sound;
            [Tooltip("The foot that this sound corresponds to.")]
            [SerializeField] protected GameObject m_Foot;

            // Exposed properties
            public AudioClip Sound { get { return m_Sound; } }
            public GameObject Foot { get { return m_Foot; } }
        }

        [Tooltip("Should a unique sound play for each foot?")]
        [SerializeField] protected bool m_PerFootSounds;
        [Tooltip("A reference to the feet which contain an AudioSource")]
        [SerializeField] protected GameObject[] m_Feet = new GameObject[0];
        [Tooltip("A list of sounds to play when the foot hits the ground")]
        [SerializeField] protected FootstepSound[] m_Footsteps = new FootstepSound[0];

        // Exposed properties
        public GameObject[] Feet { set { m_Feet = value; } }

        // Internal variables
        private List<List<AudioClip>> m_PerFootSoundList;

        // Component references
        private GameObject m_GameObject;
        private RigidbodyCharacterController m_Controller;
        private AudioSource[] m_FootAudioSource;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Controller = GetComponent<RigidbodyCharacterController>();

            // Notify each foot which index they are.
            for (int i = 0; i < m_Feet.Length; ++i) {
                var footTrigger = m_Feet[i].GetComponent<CharacterFootTrigger>();
                if (footTrigger != null) {
                    footTrigger.Index = i;
                }
            }

            m_FootAudioSource = new AudioSource[m_Feet.Length];
            for (int i = 0; i < m_Feet.Length; ++i) {
                m_FootAudioSource[i] = m_Feet[i].GetComponent<AudioSource>();
                if (m_FootAudioSource[i] == null) {
                    Debug.LogError("Error: The " + (i == 0 ? "left" : "right") + "foot does not have a AudioSource required for footsteps.");
                }
            }
            
            // Initialze the foot arrays if each foot has a specific sound.
            if (m_PerFootSounds) {
                m_PerFootSoundList = new List<List<AudioClip>>();
                for (int i = 0; i < m_Feet.Length; ++i) {
                    var footSounds = new List<AudioClip>();
                    for (int j = 0; j < m_Footsteps.Length; ++j) {
                        if (m_Footsteps[j].Foot.Equals(m_Feet[i])) {
                            footSounds.Add(m_Footsteps[i].Sound);
                        }
                    }
                    m_PerFootSoundList.Add(footSounds);
                }
            }
        }

        /// <summary>
        /// Register for any events that the footsteps should be aware of.
        /// </summary>
        private void OnEnable()
        {
            if (m_Footsteps.Length > 0) {
                EventHandler.RegisterEvent<int>(m_GameObject, "OnTriggerFootDown", PlayFootstep);
            }
        }

        /// <summary>
        /// Unregister for any events that the footsteps should be aware of.
        /// </summary>
        private void OnDisable()
        {
            if (m_Footsteps.Length > 0) {
                EventHandler.UnregisterEvent<int>(m_GameObject, "OnTriggerFootDown", PlayFootstep);
            }
        }

        /// <summary>
        /// An animation event says that a foot touched the ground - play a footstep.
        /// </summary>
        /// <param name="index">The index of hte foot that should be played.</param>
        private void PlayFootstep(int index)
        {
            // Do not play a footstep sound if the character isn't moving.
            if (m_Controller.Velocity.sqrMagnitude < 0.1f) {
                return;
            }

            m_FootAudioSource[index].clip = SoundForFoot(index);
            m_FootAudioSource[index].Play();
        }

        /// <summary>
        /// Returns the AudioClip for the specified foot.
        /// </summary>
        /// <param name="index">The index of hte foot that should be played.</param>
        /// <returns>The corresponding foot AudioClip.</returns>
        private AudioClip SoundForFoot(int index)
        {
            if (m_PerFootSounds) {
                return m_PerFootSoundList[index][Random.Range(0, m_PerFootSoundList[index].Count)];
            }
            // Each foot does not have a unique sound so return any sound within the array.
            return m_Footsteps[Random.Range(0, m_Footsteps.Length)].Sound;
        }
    }
}