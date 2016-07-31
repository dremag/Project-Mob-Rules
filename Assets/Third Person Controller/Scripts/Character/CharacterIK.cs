using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Rotates and positions the character's limbs to face in the correct direction and stand on uneven surfaces.
    /// </summary>
    public class CharacterIK : MonoBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("Draw a debug line to see the direction that the character is facing")]
        [SerializeField] protected bool m_DebugDrawLookRay;
#endif
        [Tooltip("The layers that the IK should be checking against")]
        [SerializeField] protected LayerMask m_LayerMask = LayerManager.Mask.IgnoreInvisibleLayersPlayerWater;
        [Tooltip("The speed at which the hips adjusts vertically")]
        [SerializeField] protected float m_HipsAdjustmentSpeed = 2;
        [Tooltip("The distance to look ahead")]
        [SerializeField] protected float m_LookAheadDistance = 5;
        [Tooltip("An offset to apply to the look at position")]
        [SerializeField] protected Vector3 m_LookAtOffset;
        [Tooltip("(0-1) determines how much the body is involved in the look at while aiming")]
        [SerializeField] protected float m_LookAtAimBodyWeight = 1f;
        [Tooltip("(0-1) determines how much the body is involved in the look at")]
        [SerializeField] protected float m_LookAtBodyWeight = 0.05f;
        [Tooltip("(0-1) determines how much the head is involved in the look at")]
        [SerializeField] protected float m_LookAtHeadWeight = 1.0f;
        [Tooltip("(0-1) determines how much the eyes are involved in the look at")]
        [SerializeField] protected float m_LookAtEyesWeight = 1.0f;
        [Tooltip("(0-1) 0.0 means the character is completely unrestrained in motion, 1.0 means the character motion completely clamped (look at becomes impossible)")]
        [SerializeField] protected float m_LookAtClampWeight = 0.35f;
        [Tooltip("The speed at which the look at position should adjust between using IK and not using IK")]
        [SerializeField] protected float m_LookAtIKAdjustmentSpeed = 0.4f;
        [Tooltip("(0-1) determines how much the hands look at the target")]
        [SerializeField] protected float m_HandIKWeight = 1.0f;
        [Tooltip("The speed at which the hand position/rotation should adjust between using IK and not using IK")]
        [SerializeField] protected float m_HandIKAdjustmentSpeed = 10;
        [Tooltip("The speed at which the hips position should adjust between using IK and not using IK while moving")]
        [SerializeField] protected float m_HipsMovingPositionAdjustmentSpeed = 2;
        [Tooltip("The speed at which the hips position should adjust between using IK and not using IK while still")]
        [SerializeField] protected float m_HipsStillPositionAdjustmentSpeed = 20;
        [Tooltip("The speed at which the foot position should adjust between using IK and not using IK")]
        [SerializeField] protected float m_FootPositionAdjustmentSpeed = 20;
        [Tooltip("The speed at which the foot rotation should adjust between using IK and not using IK")]
        [SerializeField] protected float m_FootRotationAdjustmentSpeed = 10;
        [Tooltip("The speed at which the foot weight should adjust")]
        [SerializeField] protected float m_FootWeightAdjustmentSpeed = 5;

        // SharedFields
        private SharedMethod<bool, float, Vector3> m_TargetLookPositionMaxDistance = null;
        private SharedMethod<int, bool> m_CanUseIK = null;
        private SharedMethod<bool> m_CanUseItem = null;
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
#if !ENABLE_MULTIPLAYER
        protected SharedMethod<bool> m_IndependentLook = null;
#endif

        // IK references
        private Transform m_Head;
        private Transform m_Hips;
        private Transform[] m_Foot;
        private Transform m_LeftHand;
        private Transform m_RightHand;
        private Transform m_DominantHand;
        private Transform m_NonDominantHand;

        // IK variables
        private float m_HipsOffset;
        private Vector3 m_HipsPosition;
        private float[] m_LegLength;
        private float[] m_LegPotentialLength;
        private float[] m_FootOffset;
        private Vector3[] m_FootPosition;
        private Quaternion[] m_FootRotation;
        private Vector3[] m_FootIKPosition;
        private Quaternion[] m_FootIKRotation;
        private float[] m_FootIKWeight;
        private int m_DominantHandIndex = -1;
        private int m_NonDominantHandIndex = -1;
        private Vector3 m_HandOffset;
        private float[] m_HandRotationWeight;
        private float m_HandPositionWeight;
        private float m_LookAtWeight;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private bool m_HasUpdated;
        private bool m_InstantMove;
        private bool m_Initialized;

        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private Animator m_Animator;
        private RigidbodyCharacterController m_Controller;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Animator = GetComponent<Animator>();
            m_Controller = GetComponent<RigidbodyCharacterController>();

            // Prevent a divide by zero.
            if (m_LookAtClampWeight == 0) {
                m_LookAtClampWeight = 0.001f;
            }

            if (m_Animator.GetBoneTransform(HumanBodyBones.Head) == null) {
                Debug.LogError("Error: The CharacterIK component can only work with humanoid models.");
                enabled = false;
                return;
            }

            // Initialize the variables used for IK.
            m_Head = m_Animator.GetBoneTransform(HumanBodyBones.Head);
            m_Hips = m_Animator.GetBoneTransform(HumanBodyBones.Hips);
            m_HipsOffset = 0;
            m_HipsPosition = m_Hips.position;
            m_Foot = new Transform[2];
            m_LegLength = new float[2];
            m_LegPotentialLength = new float[2];
            m_FootOffset = new float[2];
            m_FootPosition = new Vector3[2];
            m_FootRotation = new Quaternion[2];
            m_FootIKPosition = new Vector3[2];
            m_FootIKRotation = new Quaternion[2];
            m_FootIKWeight = new float[2];

            for (int i = 0; i < 2; ++i) {
                m_Foot[i] = m_Animator.GetBoneTransform(i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                m_FootOffset[i] = m_Foot[i].position.y - m_Transform.position.y - 0.03f;
                m_LegLength[i] = m_Hips.position.y - m_Foot[i].position.y + m_FootOffset[i];
                var bendLegth = m_Animator.GetBoneTransform(i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position.y - m_Foot[i].position.y;
                m_LegPotentialLength[i] = m_LegLength[i] + bendLegth / 2;
                m_FootPosition[i] = m_Foot[i].position;
                m_FootRotation[i] = m_Foot[i].rotation;
            }

            m_LeftHand = m_Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            m_RightHand = m_Animator.GetBoneTransform(HumanBodyBones.RightHand);
            m_HandRotationWeight = new float[2];
        }

        /// <summary>
        /// Register for any events that the IK should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", OnPrimaryItemChange);

            // Position and rotate the IK limbs immediately.
            m_InstantMove = true;
            m_HasUpdated = false;
            m_HipsPosition = m_Hips.position;
        }

        /// <summary>
        /// Unregister for any events that the IK was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
            // Independent look characters do not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
            // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
            if (!m_IndependentLook.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
        }

        /// <summary>
        /// Update the hip position after the IK loop has finished running. Note that the hip position is also updated within FixedUpdate - it is done here as well
        /// because FixedUpdate will be run in a fixed timestep while LateUpdate is framerate dependent.
        /// </summary>
        private void LateUpdate()
        {
            // When the character is on a steep slope or steps there is a chance that their feet won't be able to touch the ground because of the capsule collider.
            // Get around this restriction by lowering the hips position so the character's lower foot can touch the ground.
            m_Hips.position = m_HipsPosition;

            // Wait two frames for IK to update so it will instantly position all of the limbs.
            if (!m_HasUpdated) {
                m_HasUpdated = true;
            } else {
                m_InstantMove = false;
            }
        }

        /// <summary>
        /// Update the hip position after the IK loop has finished running. Note that the hip position is also updated within LateUpdate - it is done here as well
        /// because LateUpdate is framerate dependent while FixedUpdate will always update with OnAnimatorIK.
        /// </summary>
        private void FixedUpdate()
        {
            m_Hips.position = m_HipsPosition;
        }

        /// <summary>
        /// Update the IK position and weights.
        /// </summary>
        /// <param name="layerIndex">The animator layer that is affected by IK.</param>
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex == 0) {
                if (m_DominantHandIndex != -1) {
                    // Store the offset between hands before IK is applied. At this point the FK animation has been applied.
                    m_HandOffset = m_DominantHand.InverseTransformPoint(m_NonDominantHand.position);
                }

                // The feet should always be on the ground.
                PositionLowerBody();
                // Look in the direction that the character is aiming.
                LookAtTarget();
                if (m_HandIKWeight > 0 && m_DominantHandIndex != -1) {
                    // Rotate the dominant hand so it is facing the target.
                    RotateDominantHand();
                }
            } else if (layerIndex == 1) {
                if (m_HandIKWeight > 0 && m_DominantHandIndex != -1) {
                    // Position the non-dominant hand relative to the rotated hands. Do this in the second pass so the hands can first rotate.
                    PositionHands();
                    // Rotate the non dominant hand to be correctly rotated with the new IK position.
                    RotateNonDominantHand();
                }
            }
        }

        /// <summary>
        /// Positions the lower body so the legs are always on the ground.
        /// </summary>
        protected virtual void PositionLowerBody()
        {
            // Lowerbody IK should only be applied if the character is on the ground.
            if (m_InstantMove || (m_Controller.Grounded && m_CanUseIK.Invoke(0))) {
                var hipsOffset = 0f;

                // There are two parts to positioning the feet. The hips need to be positioned first and then the feet can be positioned. The hips need to be positioned
                // when the character is standing on uneven ground. As an example, imagine that the character is standing on a set of stairs. 
                // The stairs has two sets of colliders: one collider which covers each step, and another collider is a plane at the same slope as the stairs. 
                // When the character is standing on top of the stairs, the character’s collider is going to be resting on the plane collider while the IK system will be 
                // trying to ensure the feet are resting on the stairs collider. In some cases the plane collider may be relatively far above the stair collider so the hip 
                // needs to be moved down to allow the character’s foot to hit the stair collider.
                for (int i = 0; i < m_Foot.Length; ++i) {
                    var footPosition = m_Transform.TransformPoint(m_Transform.InverseTransformPoint(m_Foot[i].position).x, m_Transform.InverseTransformPoint(m_Hips.position).y, 0);
                    if (Physics.Raycast(footPosition, -m_Transform.up, out m_RaycastHit, m_LegPotentialLength[i], m_LayerMask)) {
                        // Do not modify the hip offset if the raycast distance is longer then the leg length. The leg wouldn't have been able to touch the ground anyway.
                        if (m_RaycastHit.distance > m_LegLength[i]) {
                            // Take the maximum offset. One leg may want to apply a shorter offset compared to the other leg and the longest offset should be used.
                            if (Mathf.Abs(m_LegLength[i] - m_RaycastHit.distance) > Mathf.Abs(hipsOffset)) {
                                hipsOffset = m_LegLength[i] - m_RaycastHit.distance;
                            }
                        }
                    }
                }

                var lowerBodyMoveSpeed = m_InstantMove ? 1 : (m_Controller.Moving ? m_HipsMovingPositionAdjustmentSpeed : m_HipsStillPositionAdjustmentSpeed) * Time.deltaTime;
                // Interpolate to the hips offset for smooth movement.
                m_HipsOffset = Mathf.Lerp(m_HipsOffset, hipsOffset, lowerBodyMoveSpeed);

                // The hip offset has been set. Do one more loop to figure out where the place the feet.
                for (int i = 0; i < m_Foot.Length; ++i) {
                    var ikGoal = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
                    var position = m_Animator.GetIKPosition(ikGoal);
                    var rotation = m_Animator.GetIKRotation(ikGoal);
                    var positionWeight = 0f;

                    var footPosition = m_Foot[i].position;
                    footPosition.y = m_Hips.position.y;
                    var footDistance = m_Hips.position.y - m_Foot[i].position.y + m_FootOffset[i] - m_HipsOffset - 0.01f;
                    // Use IK to position the feet if an object is between the hips and the bottom of the foot.
                    if (Physics.Raycast(footPosition, -m_Transform.up, out m_RaycastHit, footDistance, m_LayerMask)) {
                        var ikPosition = m_RaycastHit.point;
                        ikPosition.y += m_FootOffset[i] - m_HipsOffset;
                        m_FootIKPosition[i] = ikPosition;
                        m_FootIKRotation[i] = Quaternion.LookRotation(Vector3.Cross(m_RaycastHit.normal, rotation * -Vector3.right));

                        position = m_FootIKPosition[i];
                        rotation = m_FootIKRotation[i];
                        positionWeight = 1;
                    }

                    // Smoothly interpolate between the previous and current values to prevent jittering.
                    // Immediately move to the target value if on a moving platform.
                    m_FootPosition[i] = Vector3.Lerp(m_FootPosition[i], position, m_InstantMove ? 1 : m_FootPositionAdjustmentSpeed * Time.deltaTime);
                    m_FootRotation[i] = Quaternion.Slerp(m_FootRotation[i], rotation, m_InstantMove ? 1 : m_FootRotationAdjustmentSpeed * Time.deltaTime);
                    m_FootIKWeight[i] = Mathf.Lerp(m_FootIKWeight[i], positionWeight, m_InstantMove ? 1 : m_FootWeightAdjustmentSpeed * Time.deltaTime);

                    // Apply the IK position and rotation.
                    m_Animator.SetIKPosition(ikGoal, m_FootPosition[i]);
                    m_Animator.SetIKRotation(ikGoal, m_FootRotation[i]);
                    m_Animator.SetIKPositionWeight(ikGoal, m_FootIKWeight[i]);
                    m_Animator.SetIKRotationWeight(ikGoal, m_FootIKWeight[i]);
                }
            } else {
                // The character is not on the ground so interpolate the hips offset back to 0.
                m_HipsOffset = Mathf.Lerp(m_HipsOffset, 0, m_HipsAdjustmentSpeed * Time.deltaTime);
                // Keep updating the position and rotation values so it'll correctly interpolate when on the ground.
                for (int i = 0; i < 2; ++i) {
                    var ikGoal = i == 0 ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightFoot;
                    m_FootPosition[i] = m_Animator.GetIKPosition(ikGoal);
                    m_FootRotation[i] = m_Animator.GetIKRotation(ikGoal);
                }
            }

            m_HipsPosition = m_Hips.position;
            m_HipsPosition.y += m_HipsOffset;
        }

        /// <summary>
        /// Rotate the upper body to look at the target.
        /// </summary>
        protected virtual void LookAtTarget()
        {
            var weight = 0f;

            // Only set the look at position if the character has something to look at.
            if (m_TargetLookPositionMaxDistance != null && m_CanUseIK.Invoke(1)) {
                // Convert the direction into a position by finding a point out in the distance.
                var lookPosition = m_TargetLookPositionMaxDistance.Invoke(false, m_LookAheadDistance) + m_LookAtOffset;
                m_Animator.SetLookAtPosition(lookPosition);

                // Determine the weight to assign the look at IK by the direction of the camera and the direction of the character. If the character is facing in the
                // same direction as the camera then the look at weight should be at its max. The look at weight should smoothly move to 0 when the camera is looking
                // in the opposite direction. Instead of doing a smooth interpolation between 1 and 0 just based on the dot product, the interpolation to 0 should start
                // at 1 minus the clamp weight. This allows the character to still turn their head and body to the side without the weight decreasing.
                var forwardDirection = m_Transform.forward;
                var lookDirection = (lookPosition - m_Head.position).normalized;

                // Ignore the y direction.
                lookDirection.y = 0;
                forwardDirection.y = 0;

                // Determine the normalized dot product.
                var dotProduct = Vector3.Dot(m_Transform.forward.normalized, lookDirection.normalized);
                var weightFactor = 1 / m_LookAtClampWeight;

                // Use the slope intercept forumla to determine the weight. The weight should have its maximum value when the dot product is greater than 1 minus the clamp wieght,
                // and smoothly transition to 0 as the dot product gets closer to -1.
                weight = Mathf.Lerp(0, 1, Mathf.Clamp01(weightFactor * dotProduct + weightFactor));

#if UNITY_EDITOR
                // Visualize the direction of the target look position.
                if (m_DebugDrawLookRay) {
                    Debug.DrawLine(m_Animator.GetIKPosition(m_DominantHandIndex == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand),
                                        m_TargetLookPositionMaxDistance.Invoke(false, m_LookAheadDistance), Color.red);
                }
#endif
            }

            // Finally apply the weight.
            m_LookAtWeight = Mathf.Lerp(m_LookAtWeight, weight, m_InstantMove ? 1 : m_LookAtIKAdjustmentSpeed * Time.deltaTime);
            m_Animator.SetLookAtWeight(m_LookAtWeight, (m_Controller.Aiming ? m_LookAtAimBodyWeight : m_LookAtBodyWeight), m_LookAtHeadWeight, m_LookAtEyesWeight, m_LookAtClampWeight);
        }

        /// <summary>
        /// If the character is aiming, rotate the the dominant hand to face the target.
        /// </summary>
        protected virtual void RotateDominantHand()
        {
            var rotationWeight = m_InstantMove ? 1f : 0f;
            var dominantHandIKGoal = m_DominantHandIndex == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;

            // Only set the arm position if the character is aiming and has something to look at.
            if (m_Controller.Aiming && m_TargetLookPositionMaxDistance != null && m_CanUseItem.Invoke()) {
                // The IK should be fully active.
                rotationWeight = 1;

                // Get a point that is slightly in front of the player. Don't go too far because then the point will almost be directly in front of the character
                // whereas the crosshairs is off to the side.
                var lookDirection = (m_TargetLookPositionMaxDistance.Invoke(true, m_LookAheadDistance) - m_Animator.GetIKPosition(dominantHandIKGoal) + m_LookAtOffset).normalized;

                // Get the x and y delta angle to determine how much the hand needs to rotate.
                var lookDirectionEulerAngles = Quaternion.LookRotation(lookDirection).eulerAngles;
                var ikEulerAngles = m_Animator.GetIKRotation(dominantHandIKGoal).eulerAngles;
                var yOffset = m_Transform.eulerAngles.y - ikEulerAngles.y; // Correct for the dominant hand may not always facing in the forward direction.
                var xDeltaAngle = Mathf.DeltaAngle(ikEulerAngles.x, lookDirectionEulerAngles.x);
                var yDeltaAngle = Mathf.DeltaAngle(ikEulerAngles.y, lookDirectionEulerAngles.y) - yOffset;

                // Rotate the dominant hand on the Transform's right axis to face in the direction of the target. This will tilt
                // the hand up/down depending on the pitch of the camera. Rotate on the y axis as well to face in the direction of the target.
                var targetRotation = Quaternion.AngleAxis(xDeltaAngle, m_Transform.right) * Quaternion.AngleAxis(yDeltaAngle, m_Transform.up) * m_Animator.GetIKRotation(dominantHandIKGoal);

                // Set the IK rotation.
                m_Animator.SetIKRotation(dominantHandIKGoal, targetRotation);
            }

            // Smoothly interpolate and set the IK rotation weight.
            m_HandRotationWeight[m_DominantHandIndex] = Mathf.Lerp(m_HandRotationWeight[m_DominantHandIndex], rotationWeight, m_InstantMove ? 1 : m_HandIKAdjustmentSpeed * Time.deltaTime);
            m_Animator.SetIKRotationWeight(dominantHandIKGoal, m_HandRotationWeight[m_DominantHandIndex] * m_HandIKWeight);
        }

        /// <summary>
        /// Rotates the non-dominant hand to look at the target.
        /// </summary>
        /// <param name="imediatePosition">Should the rotation be applied immediately?</param>
        protected virtual void RotateNonDominantHand()
        {
            var rotationWeight = m_InstantMove ? 1f : 0f;
            var nonDominantHandIKGoal = m_NonDominantHandIndex == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;

            if (m_Controller.Aiming && m_TargetLookPositionMaxDistance != null && m_CanUseItem.Invoke() && m_CurrentPrimaryItem.Get() != null && m_CurrentPrimaryItem.Get().TwoHandedItem) {
                // The IK should be fully active.
                rotationWeight = 1;

                // Similar to the dominant hand, rotate the non-dominant hand on the Transform's right axis. Do not adjust the y rotation because the second hand may have a
                // rotation to it on the y axis.
                var lookDirection = (m_TargetLookPositionMaxDistance.Invoke(true, m_LookAheadDistance) - m_Animator.GetIKPosition(nonDominantHandIKGoal) + m_LookAtOffset).normalized;

                // Get the x and y delta angle to determine how much the hand needs to rotate.
                var lookDirectionEulerAngles = Quaternion.LookRotation(lookDirection).eulerAngles;
                var ikEulerAngles = m_Animator.GetIKRotation(nonDominantHandIKGoal).eulerAngles;
                var xDeltaAngle = Mathf.DeltaAngle(ikEulerAngles.x, lookDirectionEulerAngles.x);

                // Rotate the dominant hand on the Transform's right axis to face in the direction of the target. This will tilt
                // the hand up/down depending on the pitch of the camera.
                var targetRotation = Quaternion.AngleAxis(xDeltaAngle, m_Transform.right) * m_Animator.GetIKRotation(nonDominantHandIKGoal);

                // Set the IK rotation.
                m_Animator.SetIKRotation(nonDominantHandIKGoal, targetRotation);
            }

            m_HandRotationWeight[m_NonDominantHandIndex] = Mathf.Lerp(m_HandRotationWeight[m_NonDominantHandIndex], rotationWeight, m_InstantMove ? 1 : m_HandIKAdjustmentSpeed * Time.deltaTime);
            m_Animator.SetIKRotationWeight(nonDominantHandIKGoal, m_HandRotationWeight[m_NonDominantHandIndex] * m_HandIKWeight);
        }

        /// <summary>
        /// If the character is aiming, position the hands so they are in the same relative position compared to the rotated hands.
        /// </summary>
        protected virtual void PositionHands()
        {
            var positionWeight = m_InstantMove ? 1f : 0f;
            var nonDominantHandIKGoal = m_NonDominantHandIndex == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;

            if (m_Controller.Aiming && m_CanUseItem.Invoke() && m_CurrentPrimaryItem.Get() != null && m_CurrentPrimaryItem.Get().TwoHandedItem) {
                // The IK should be fully active.
                positionWeight = 1;
            }

            // Set the position of the hand so it is always relative to the rotated dominant hand.
            m_Animator.SetIKPosition(nonDominantHandIKGoal, m_DominantHand.TransformPoint(m_HandOffset));

            // Smoothly interpolate and set the IK rotation weights.
            m_HandPositionWeight = Mathf.Lerp(m_HandPositionWeight, positionWeight, m_InstantMove ? 1 : m_HandIKAdjustmentSpeed * Time.deltaTime);
            m_Animator.SetIKPositionWeight(nonDominantHandIKGoal, m_HandPositionWeight * m_HandIKWeight);
        }

        /// <summary>
        /// The primary item has been changed. Update the dominant hand.
        /// </summary>
        /// <param name="item">The new item. Can be null.</param>
        private void OnPrimaryItemChange(Item item)
        {
            if (item != null) {
                var handTransform = item.HandTransform;
                m_DominantHandIndex = handTransform.Equals(m_LeftHand) ? 0 : 1;
                m_NonDominantHandIndex = m_DominantHandIndex == 0 ? 1 : 0;
                m_DominantHand = handTransform;
                m_NonDominantHand = m_DominantHandIndex == 1 ? m_LeftHand : m_RightHand;
            } else {
                m_DominantHandIndex = m_NonDominantHandIndex = -1;
                m_DominantHand = m_NonDominantHand = null;
            }
        }

        /// <summary>
        /// The character has died. Disable the IK.
        /// </summary>
        private void OnDeath()
        {
            enabled = false;

            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }

        /// <summary>
        /// The character has respawned. Enable the IK.
        /// </summary>
        private void OnRespawn()
        {
            enabled = true;

            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }
    }
}