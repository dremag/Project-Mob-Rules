using UnityEngine;
using UnityEngine.UI;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The CrosshairsMonitor will keep the crosshairs UI in sync with the rest of the game. This includes showing the correct crosshairs type and accounting for any recoil.
    /// </summary>
    public class CrosshairsMonitor : MonoBehaviour
    {
        [Tooltip("The normal color of the crosshairs")]
        [SerializeField] protected Color m_CrosshairsColor;
        [Tooltip("The color of the crosshairs when the character is targeting an enemy")]
        [SerializeField] protected Color m_CrosshairsTargetColor;
        [Tooltip("The layer mask of the enemy")]
        [SerializeField] protected LayerMask m_CrosshairsTargetLayer;
        [Tooltip("The image for the left crosshairs")]
        [SerializeField] protected Image m_LeftCrosshairsImage;
        [Tooltip("The image for the top crosshairs")]
        [SerializeField] protected Image m_TopCrosshairsImage;
        [Tooltip("The image for the right crosshairs")]
        [SerializeField] protected Image m_RightCrosshairsImage;
        [Tooltip("The image for the bottom crosshairs")]
        [SerializeField] protected Image m_BottomCrosshairsImage;
        [Tooltip("The sprite used when no item is active")]
        [SerializeField] protected Sprite m_NoItemSprite;
        [Tooltip("The distance to start scaling the crosshairs")]
        [SerializeField] protected float m_NearCrosshairsDistance;
        [Tooltip("The distance to end scaling the crosshairs")]
        [SerializeField] protected float m_FarCrosshairsDistance;
        [Tooltip("The scale of the crosshairs when the crosshairs hits a near object")]
        [SerializeField] protected Vector3 m_NearCrosshairsScale = Vector3.one;
        [Tooltip("The scale of the crosshairs when the crosshairs hits a far object")]
        [SerializeField] protected Vector3 m_FarCrosshairsScale = Vector3.one;
        [Tooltip("Is the crosshairs only visible when the character is aiming?")]
        [SerializeField] protected bool m_OnlyVisibleOnAim;

        // SharedFields
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedMethod<bool, Vector3> m_TargetLookDirection = null;
        
        // Internal variables
        private float m_RecoilAmount;
        private RaycastHit m_RaycastHit;
        private bool m_ShowSideSprites;

        // Component references
        private GameObject m_GameObject;
        private Image m_Image;
        private RectTransform m_RectTransform;
        private Camera m_Camera;
        private CameraMonitor m_CameraMonitor;
        private GameObject m_Character;
        private RigidbodyCharacterController m_Controller;

        private RectTransform m_ImageRectTransform;
        private RectTransform m_LeftCrosshairsRectTransform;
        private RectTransform m_TopCrosshairsRectTransform;
        private RectTransform m_RightCrosshairsRectTransform;
        private RectTransform m_BottomCrosshairsRectTransform;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_RectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();

            m_ImageRectTransform = m_Image.GetComponent<RectTransform>();
            m_LeftCrosshairsRectTransform = m_LeftCrosshairsImage.GetComponent<RectTransform>();
            m_TopCrosshairsRectTransform = m_TopCrosshairsImage.GetComponent<RectTransform>();
            m_RightCrosshairsRectTransform = m_RightCrosshairsImage.GetComponent<RectTransform>();
            m_BottomCrosshairsRectTransform = m_BottomCrosshairsImage.GetComponent<RectTransform>();

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the crosshairs.
            EnableCrosshairsImage(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (m_Character != character && m_Character != null) {
                EventHandler.UnregisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                EventHandler.UnregisterEvent<bool>(m_Character, "OnAllowGameplayInput", AllowGameplayInput);
                EventHandler.UnregisterEvent<float>(m_Character, "OnCameraUpdateRecoil", UpdateRecoil);
                EventHandler.UnregisterEvent<bool>(m_Character, "OnLaserSightUseableLaserSightActive", DisableCrosshairs);
                EventHandler.UnregisterEvent<bool>(m_Character, "OnItemShowScope", DisableCrosshairs);
                if (m_OnlyVisibleOnAim) {
                    EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
                }
            }

            m_Character = character;
            if (m_Character == null || !m_GameObject.activeInHierarchy) {
                // The object may be destroyed when Unity is ending.
                if (this != null) {
                    EnableCrosshairsImage(false);
                }
                return;
            }

            SharedManager.InitializeSharedFields(m_Character, this);
            m_Controller = m_Character.GetComponent<RigidbodyCharacterController>();
            if (m_Camera == null) {
                m_Camera = Utility.FindCamera();
                m_CameraMonitor = m_Camera.GetComponent<CameraMonitor>();
                m_CameraMonitor.Crosshairs = transform;
                SharedManager.InitializeSharedFields(m_Camera.gameObject, this);
            }

            PrimaryItemChange(m_CurrentPrimaryItem.Get());

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
            EventHandler.RegisterEvent<bool>(m_Character, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<float>(m_Character, "OnCameraUpdateRecoil", UpdateRecoil);
            EventHandler.RegisterEvent<bool>(m_Character, "OnLaserSightUseableLaserSightActive", DisableCrosshairs);
            EventHandler.RegisterEvent<bool>(m_Character, "OnItemShowScope", DisableCrosshairs);
            if (m_OnlyVisibleOnAim) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }

            EnableCrosshairsImage(!m_OnlyVisibleOnAim || m_Controller.Aiming);
        }

        /// <summary>
        /// Attach the set character. It is ok if the same character is being attached again.
        /// </summary>
        private void OnEnable()
        {
            AttachCharacter(m_Character);
        }

        /// <summary>
        /// Change the color of the crosshairs when the camera is looking at an object within the crosshairs target layer.
        /// </summary>
        private void Update()
        {
            // The look direction may not have been set yet if the crosshairs component exists but no character exists.
            if (m_TargetLookDirection == null) {
                return;
            }

            // Turn the GUI the target color if a target was hit.
            Color crosshairsColor;
            var crosshairsRay = m_Camera.ScreenPointToRay(m_RectTransform.position);
            if (Physics.Raycast(crosshairsRay, out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer | m_CrosshairsTargetLayer.value)) {
                // Change to the target color if the raycast hit the target layer.
                if (Utility.InLayerMask(m_RaycastHit.transform.gameObject.layer, m_CrosshairsTargetLayer.value)) {
                    crosshairsColor = m_CrosshairsTargetColor;
                    m_CameraMonitor.TargetLock = m_RaycastHit.transform;
                } else {
                    crosshairsColor = m_CrosshairsColor;
                    m_CameraMonitor.TargetLock = null;
                }
                // Dynamically change the scale of the crosshairs based on the distance of the object hit.
                if (m_FarCrosshairsDistance != m_NearCrosshairsDistance) {
                    var scale = Vector3.Lerp(m_NearCrosshairsScale, m_FarCrosshairsScale, (m_RaycastHit.distance - m_NearCrosshairsDistance) / (m_FarCrosshairsDistance - m_NearCrosshairsDistance));
                    m_ImageRectTransform.localScale = scale;
                }
            } else {
                crosshairsColor = m_CrosshairsColor;
            }
            // Set the color of all of the crosshairs images.
            m_Image.color = m_LeftCrosshairsImage.color = m_TopCrosshairsImage.color = m_RightCrosshairsImage.color = m_BottomCrosshairsImage.color = crosshairsColor;
        }

        /// <summary>
        /// The primary item has been changed. Update the crosshairs to reflect this new item.
        /// </summary>
        /// <param name="item">The new item. Can be null.</param>
        private void PrimaryItemChange(Item item)
        {
            if (m_Image == null) {
                return;
            }

            CrosshairsType crosshairs = null;
            if (item == null) {
                m_Image.sprite = m_NoItemSprite;
            } else {
                crosshairs = item.CrosshairsSprite;
                m_Image.sprite = crosshairs.Center;
            }
            // Change the size of the crosshairs image according to the size of the sprite.
            SizeSprite(m_Image.sprite, m_RectTransform);

            if (crosshairs != null) {
                m_ShowSideSprites = crosshairs.Left != null;
                if (m_ShowSideSprites) {
                    // Assign and position/size the left crosshairs.
                    m_LeftCrosshairsImage.sprite = crosshairs.Left;
                    PositionSprite(m_LeftCrosshairsRectTransform, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset), 0);
                    SizeSprite(m_LeftCrosshairsImage.sprite, m_LeftCrosshairsRectTransform);

                    // Assign and position/size the top crosshairs.
                    m_TopCrosshairsImage.sprite = crosshairs.Top;
                    PositionSprite(m_TopCrosshairsRectTransform, 0, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset);
                    SizeSprite(m_TopCrosshairsImage.sprite, m_TopCrosshairsRectTransform);

                    // Assign and position/size the right crosshairs.
                    m_RightCrosshairsImage.sprite = crosshairs.Right;
                    PositionSprite(m_RightCrosshairsRectTransform, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset, 0);
                    SizeSprite(m_RightCrosshairsImage.sprite, m_RightCrosshairsRectTransform);

                    // Assign and position/size the bottom crosshairs.
                    m_BottomCrosshairsImage.sprite = crosshairs.Bottom;
                    PositionSprite(m_BottomCrosshairsRectTransform, 0, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset));
                    SizeSprite(m_BottomCrosshairsImage.sprite, m_BottomCrosshairsRectTransform);
                }
            } else {
                m_ShowSideSprites = false;
            }

            if (!m_ShowSideSprites) {
                m_LeftCrosshairsImage.sprite = m_TopCrosshairsImage.sprite = m_RightCrosshairsImage.sprite = m_BottomCrosshairsImage.sprite = null;
            }
            EnableCrosshairsImage(m_Image.sprite != null && (!m_OnlyVisibleOnAim || (m_Controller != null && m_Controller.Aiming)));
        }

        /// <summary>
        /// Positions the sprite according to the specified x and y position.
        /// </summary>
        /// <param name="spriteRectTransform">The transform to position.</param>
        /// <param name="xPosition">The x position of the sprite.</param>
        /// <param name="yPosition">The y position of the sprite.</param>
        private void PositionSprite(RectTransform spriteRectTransform, float xPosition, float yPosition)
        {
            var position = spriteRectTransform.localPosition;
            position.x = xPosition;
            position.y = yPosition;
            spriteRectTransform.localPosition = position;
        }

        /// <summary>
        /// Change the size of the RectTransform according to the size of the sprite.
        /// </summary>
        /// <param name="sprite">The sprite that the RectTransform should change its size to.</param>
        /// <param name="spriteRectTransform">A reference to the RectTransform.</param>
        private void SizeSprite(Sprite sprite, RectTransform spriteRectTransform)
        {
            if (sprite != null) {
                var sizeDelta = spriteRectTransform.sizeDelta;
                sizeDelta.x = sprite.textureRect.width;
                sizeDelta.y = sprite.textureRect.height;
                spriteRectTransform.sizeDelta = sizeDelta;
            }
        }

        /// <summary>
        /// The character has fired their weapon and a recoil has been added. Move the directional crosshair images according to that recoil amount.
        /// </summary>
        /// <param name="recoilAmount">The amount of recoil to apply.</param>
        private void UpdateRecoil(float recoilAmount)
        {
            // No need to apply recoil if there is no item.
            var primaryItem = m_CurrentPrimaryItem.Get();
            if (primaryItem == null) {
                return;
            }

            m_RecoilAmount = recoilAmount;
            var crosshairs = primaryItem.CrosshairsSprite;
            
            // The directional crosshairs should change position according to the amount of recoil.
            if (crosshairs.Left != null) {
                PositionSprite(m_LeftCrosshairsRectTransform, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset), 0);
                PositionSprite(m_TopCrosshairsRectTransform, 0, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset);
                PositionSprite(m_RightCrosshairsRectTransform, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset, 0);
                PositionSprite(m_BottomCrosshairsRectTransform, 0, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset));
            }
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            EnableCrosshairsImage(allow && (!m_OnlyVisibleOnAim || m_Controller.Aiming));
        }

        /// <summary>
        /// Should the crosshairs be disabled?
        /// </summary>
        /// <param name="disable">True if the crosshairs should be disabled.</param>
        private void DisableCrosshairs(bool disable)
        {
            // The crosshairs may still need to be disabled if the crosshairs is only visible when aiming and the character is not aiming.
            if (!disable) {
                disable = m_OnlyVisibleOnAim && !m_Controller.Aiming;
            }
            EnableCrosshairsImage(!disable);
        }

        /// <summary>
        /// Enables or disables the crosshairs images.
        /// </summary>
        /// <param name="enable">Should the crosshairs images be enabled?</param>
        private void EnableCrosshairsImage(bool enable)
        {
            if (m_Image != null) {
                m_Image.enabled = m_Image.sprite != null && enable;
            }

            var hasSectionalImage = false;
            if (m_LeftCrosshairsImage != null) {
                m_LeftCrosshairsImage.enabled = m_LeftCrosshairsImage.sprite != null && enable;
                hasSectionalImage = true;
            }
            if (m_RightCrosshairsImage != null) {
                m_RightCrosshairsImage.enabled = m_RightCrosshairsImage.sprite != null && enable;
                hasSectionalImage = true;
            }
            if (m_TopCrosshairsImage != null) {
                m_TopCrosshairsImage.enabled = m_TopCrosshairsImage.sprite != null && enable;
                hasSectionalImage = true;
            }
            if (m_BottomCrosshairsImage != null) {
                m_BottomCrosshairsImage.enabled = m_BottomCrosshairsImage.sprite != null && enable;
                hasSectionalImage = true;
            }
            if (hasSectionalImage && gameObject != null) {
                enable = enable && m_ShowSideSprites;
            }
        }

        /// <summary>
        /// Shows or hides the UI.
        /// </summary>
        /// <param name="show">Should the UI be shown?</param>
        private void ShowUI(bool show)
        {
            m_GameObject.SetActive(show);
        }

        /// <summary>
        /// The EventHandler was cleared. This will happen when a new scene is loaded. Unregister the registered events to prevent old events from being fired.
        /// </summary>
        private void EventHandlerClear()
        {
            EventHandler.UnregisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.UnregisterEvent<bool>("OnShowUI", ShowUI);
            EventHandler.UnregisterEvent("OnEventHandlerClear", EventHandlerClear);
        }

        /// <summary>
        /// Callback when the cahracter starts or stops aiming.
        /// </summary>
        /// <param name="aim">Is the character aiming?</param>
        private void OnAim(bool aim)
        {
            EnableCrosshairsImage(aim);
        }
    }
}