using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Initializes an object which slowly fades out with time. Can optionally attach a light to the GameObject and that light will be faded as well.
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        [Tooltip("The alpha value to initialize the muzzle flash material to")]
        [SerializeField] protected float m_StartAlpha = 0.5f;
        [Tooltip("The minimum fade speed - the larger the value the quicker the muzzle flash will fade")]
        [SerializeField] protected float m_MinFadeSpeed = 1.25f;
        [Tooltip("The maximum fade speed - the larger the value the quicker the muzzle flash will fade")]
        [SerializeField] protected float m_MaxFadeSpeed = 1.25f;

        // Internal variables
        private const string TintColor = "_TintColor";
        private Color m_Color;
        private float m_StartLightIntensity;
        private float m_FadeSpeed;

        // Component references
        private GameObject m_GameObject;
        private Material m_Material;
        private Light m_Light;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Material = GetComponent<Renderer>().sharedMaterial;
            m_Light = GetComponent<Light>();
            // If a light exists set the start light intensity. Every time the muzzle flash is enabed the light intensity will be reset to its starting value.
            if (m_Light != null) {
                m_StartLightIntensity = m_Light.intensity;
            }
        }

        /// <summary>
        /// Initialize the starting values.
        /// </summary>
        private void OnEnable()
        {
            // The muzzle flash may still be active when it is disabled. Set the alpha value to 0 to prevent the muzzle flash from appearing if the character spawns again.
            m_Color.a = 0;
            m_Material.SetColor(TintColor, m_Color);
            if (m_Light != null) {
                m_Light.intensity = 0;
            }
        }

        /// <summary>
        /// A weapon has been fired and the muzzle flash needs to show. Set the starting alpha value and light intensity if the light exists.
        /// </summary>
        public void Show()
        {
            m_Color = Color.white;
            m_Color.a = m_StartAlpha;
            m_Material.SetColor(TintColor, m_Color);
            m_FadeSpeed = Random.Range(m_MinFadeSpeed, m_MaxFadeSpeed);
            if (m_Light != null) {
                m_Light.intensity = m_StartLightIntensity;
            }
        }

        /// <summary>
        /// Decrease the alpha value of the muzzle flash to give it a fading effect. As soon as the alpha value reaches zero place the muzzle flash back in
        /// the object pool. If a light exists decrease the intensity of the light as well.
        /// </summary>
        private void Update()
        {
            if (m_Color.a > 0) {
                m_Color.a -= m_FadeSpeed * Time.deltaTime;
                m_Material.SetColor(TintColor, m_Color);
                // Keep the light intensity synchronized with the alpha channel's value.
                if (m_Light != null) {
                    m_Light.intensity = m_StartLightIntensity * (m_Color.a / m_StartAlpha);
                }
            } else {
                ObjectPool.Destroy(m_GameObject);
            }
        }
    }
}