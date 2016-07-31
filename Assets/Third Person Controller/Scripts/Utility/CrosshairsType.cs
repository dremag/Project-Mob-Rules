using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Stores the crosshairs variables.
    /// </summary>
    [System.Serializable]
    public class CrosshairsType
    {
        [Tooltip("The center crosshairs sprite (optional)")]
        [SerializeField] protected Sprite m_Center;
        [Tooltip("The offset of the remaining crosshairs textures")]
        [SerializeField] protected float m_Offset;
        [Tooltip("The left crosshairs sprite")]
        [SerializeField] protected Sprite m_Left;
        [Tooltip("The top crosshairs sprite")]
        [SerializeField] protected Sprite m_Top;
        [Tooltip("The right crosshairs sprite")]
        [SerializeField] protected Sprite m_Right;
        [Tooltip("The bottom crosshairs sprite")]
        [SerializeField] protected Sprite m_Bottom;
        [Tooltip("How much of an offset is applied to the left, top, right, and bottom crosshairs when there is recoil")]
        [SerializeField] protected float m_AccuracyLossPercent = 0.05f;

        // Exposed properties
        public Sprite Center { get { return m_Center; } }
        public float Offset { get { return m_Offset; } }
        public Sprite Left { get { return m_Left; } }
        public Sprite Top { get { return m_Top; } }
        public Sprite Right { get { return m_Right; } }
        public Sprite Bottom { get { return m_Bottom; } }
        public float AccuracyLossPercent { get { return m_AccuracyLossPercent; } }
    }
}