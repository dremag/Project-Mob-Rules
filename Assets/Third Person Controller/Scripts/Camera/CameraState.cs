using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The CameraState specifies the settings for the CameraController.
    /// </summary>
    public class CameraState : ScriptableObject
    {
        [Tooltip("Is the camera state exclusive of all of the other states?")]
        [SerializeField] protected bool m_Exclusive;

        [Tooltip("Should the view mode state be applied?")]
        [SerializeField] protected bool m_ApplyViewMode;
        [Tooltip("The view mode to use")]
        [SerializeField] protected CameraMonitor.CameraViewMode m_ViewMode;

        [Tooltip("Should the pitch limit state be applied?")]
        [SerializeField] protected bool m_ApplyPitchLimit;
        [Tooltip("The minimum pitch angle (in degrees)")]
        [SerializeField] protected float m_MinPitchLimit = -60;
        [Tooltip("The maximum pitch angle (in degrees)")]
        [SerializeField] protected float m_MaxPitchLimit = 70;
        [Tooltip("Should the yaw limit state be applied?")]
        [SerializeField] protected bool m_ApplyYawLimit;
        [Tooltip("The minimum yaw angle while in cover (in degrees)")]
        [SerializeField] protected float m_MinYawLimit = -180;
        [Tooltip("The maximum yaw angle while in cover (in degrees)")]
        [SerializeField] protected float m_MaxYawLimit = 180;
        [Tooltip("Should the ignore layer mask state be applied?")]
        [SerializeField] protected bool m_ApplyIgnoreLayerMask;
        [Tooltip("Ignore the specified layers when determining if the camera view is being obstructed")]
        [SerializeField] protected LayerMask m_IgnoreLayerMask = LayerManager.Mask.IgnoreInvisibleLayersPlayerWater;
        
        [Tooltip("Should the move smoothing state be applied?")]
        [SerializeField] protected bool m_ApplyMoveSmoothing;
        [Tooltip("The amount of smoothing to apply to the movement. Can be zero")]
        [SerializeField] protected float m_MoveSmoothing = 0.1f;
        [Tooltip("Should the camera offset state be applied?")]
        [SerializeField] protected bool m_ApplyCameraOffset;
        [Tooltip("The offset between the anchor and the location of the camera")]
        [SerializeField] protected Vector3 m_CameraOffset = new Vector3(0.5f, 0.9f, -2f);
        [Tooltip("Should the field of view state be applied?")]
        [SerializeField] protected bool m_ApplyFieldOfView;
        [Tooltip("The camera field of view")]
        [SerializeField] protected float m_FieldOfView = 60;
        [Tooltip("The speed at which the FOV transitions field of views")]
        [SerializeField] protected float m_FieldOfViewSpeed = 5;
        [Tooltip("Should the turn state be applied?")]
        [SerializeField] protected bool m_ApplyTurn;
        [Tooltip("The amount of smoothing to apply to the pitch and yaw. Can be zero")]
        [SerializeField] protected float m_TurnSmoothing = 0.05f;
        [Tooltip("The speed at which the camera turns")]
        [SerializeField] protected float m_TurnSpeed = 1.5f;
        
        [Tooltip("Should the rotation speed state be applied?")]
        [SerializeField] protected bool m_ApplyRotationSpeed;
        [Tooltip("The rotation speed when not using the third person view")]
        [SerializeField] protected float m_RotationSpeed = 1.5f;
        [Tooltip("Should the view state be applied?")]
        [SerializeField] protected bool m_ApplyView;
        [Tooltip("The distance to position the camera away from the anchor when not in third person view")]
        [SerializeField] protected float m_ViewDistance = 10;
        [Tooltip("The number of degrees to adjust if the anchor is obstructed by an object when not in third person view")]
        [SerializeField] protected float m_ViewStep = 5;
        [Tooltip("The 2.5D target look direction")]
        [SerializeField] protected Vector3 m_LookDirection = Vector3.forward;
        
        [Tooltip("Should the step zoom state be applied?")]
        [SerializeField] protected bool m_ApplyStepZoom;
        [Tooltip("The sensitivity of the step zoom")]
        [SerializeField] protected float m_StepZoomSensitivity;
        [Tooltip("The minimum amount that the camera can step zoom")]
        [SerializeField] protected float m_MinStepZoom;
        [Tooltip("The maximum amount that the camera can step zoom")]
        [SerializeField] protected float m_MaxStepZoom;
        
        [Tooltip("Should the collision radius state be applied?")]
        [SerializeField] protected bool m_ApplyCollisionRadius;
        [Tooltip("The radius of the camera's collision sphere to prevent it from clipping with other objects")]
        [SerializeField] protected float m_CollisionRadius = 0.01f;
        [Tooltip("Should the fade character state be applied?")]
        [SerializeField] protected bool m_ApplyFadeCharacter;
        [Tooltip("Fade the character's material when the camera gets too close to the character. This will prevent the camera from clipping with the character")]
        [SerializeField] protected bool m_FadeCharacter;
        [Tooltip("The distance that the character starts to fade")]
        [SerializeField] protected float m_StartFadeDistance = 2;
        [Tooltip("The distance that the character is completely invisible")]
        [SerializeField] protected float m_EndFadeDistance = 1;
        
        [Tooltip("Should the target lock state be applied?")]
        [SerializeField] protected bool m_ApplyTargetLock;
        [Tooltip("Should the crosshairs lock onto enemies?")]
        [SerializeField] protected bool m_UseTargetLock;
        [Tooltip("If target lock is enabled, specifies how quickly to move to the target (0 - 1)")]
        [SerializeField] protected float m_TargetLockSpeed = 0.95f;
        [Tooltip("If target lock is enabled, specifies how much force is required to break the lock")]
        [SerializeField] protected float m_BreakForce = 2;
        [Tooltip("If target lock is enabled and the target is a humanoid, should the target  lock onto a specific bone?")]
        [SerializeField] protected bool m_UseHumanoidTargetLock;
        [Tooltip("If target lock is enabled and the target is a humanoid, specifies which bone to lock onto")]
        [SerializeField] protected HumanBodyBones m_HumanoidTargetLockBone;
        
        [Tooltip("Should the recoil state be applied?")]
        [SerializeField] protected bool m_ApplyRecoil;
        [Tooltip("The speed at which the recoil increases when the weapon is initially fired")]
        [SerializeField] protected float m_RecoilSpring = 0.01f;
        [Tooltip("The speed at which the recoil decreases after the recoil has hit its peak and is settling back to its original value")]
        [SerializeField] protected float m_RecoilDampening = 0.05f;

        [Tooltip("Should the obstruction check be applied?")]
        [SerializeField] protected bool m_ApplyObstructionCheck;
        [Tooltip("Should the camera perform an obstruction check?")]
        [SerializeField] protected bool m_ObstructionCheck;
        [Tooltip("Should the static height state be applied?")]
        [SerializeField] protected bool m_ApplyStaticHeight;
        [Tooltip("Should the y-position be a static value?")]
        [SerializeField] protected bool m_StaticHeight;
        [Tooltip("Should the vertical offset state be applied?")]
        [SerializeField] protected bool m_ApplyVerticalOffset;
        [Tooltip("The amount of vertical offset to apply")]
        [SerializeField] protected float m_VerticalOffset;

        public bool Exclusive { get { return m_Exclusive; } }

        public bool ApplyViewMode { get { return m_ApplyViewMode; } }
        public CameraMonitor.CameraViewMode ViewMode { get { return m_ViewMode; } set { m_ViewMode = value; } }

        public bool ApplyPitchLimit { get { return m_ApplyPitchLimit; } }
        public float MinPitchLimit { get { return m_MinPitchLimit; } set { m_MinPitchLimit = value; } }
        public float MaxPitchLimit { get { return m_MaxPitchLimit; } set { m_MaxPitchLimit = value; } }
        public bool ApplyYawLimit { get { return m_ApplyYawLimit; } }
        public float MinYawLimit { get { return m_MinYawLimit; } set { m_MinYawLimit = value; } }
        public float MaxYawLimit { get { return m_MaxYawLimit; } set { m_MaxYawLimit = value; } }
        public bool ApplyIgnoreLayerMask { get { return m_ApplyIgnoreLayerMask; } }
        public LayerMask IgnoreLayerMask { get { return m_IgnoreLayerMask; } set { m_IgnoreLayerMask = value; } }

        public bool ApplyMoveSmoothing { get { return m_ApplyMoveSmoothing; } }
        public float MoveSmoothing { get { return m_MoveSmoothing; } set { m_MoveSmoothing = value; } }
        public bool ApplyCameraOffset { get { return m_ApplyCameraOffset; } }
        public Vector3 CameraOffset { get { return m_CameraOffset; } set { m_CameraOffset = value; } }
        public bool ApplyFieldOfView { get { return m_ApplyFieldOfView; } }
        public float FieldOfView { get { return m_FieldOfView; } set { m_FieldOfView = value; } }
        public float FieldOfViewSpeed { get { return m_FieldOfViewSpeed; } set { m_FieldOfViewSpeed = value; } }
        public bool ApplyTurn { get { return m_ApplyTurn; } }
        public float TurnSmoothing { get { return m_TurnSmoothing; } set { m_TurnSmoothing = value; } }
        public float TurnSpeed { get { return m_TurnSpeed; } set { m_TurnSpeed = value; } }

        public bool ApplyRotationSpeed { get { return m_ApplyRotationSpeed; } }
        public float RotationSpeed { get { return m_RotationSpeed; } set { m_RotationSpeed = value; } }
        public bool ApplyView { get { return m_ApplyView; } }
        public float ViewDistance { get { return m_ViewDistance; } set { m_ViewDistance = value; } }
        public float ViewStep { get { return m_ViewStep; } set { m_ViewStep = value; } }
        public Vector3 LookDirection { get { return m_LookDirection; } set { m_LookDirection = value; } }

        public bool ApplyStepZoom { get { return m_ApplyStepZoom; } }
        public float StepZoomSensitivity { get { return m_StepZoomSensitivity; } set { m_StepZoomSensitivity = value; } }
        public float MinStepZoom { get { return m_MinStepZoom; } set { m_MinStepZoom = value; } }
        public float MaxStepZoom { get { return m_MaxStepZoom; } set { m_MaxStepZoom = value; } }

        public bool ApplyCollisionRadius { get { return m_ApplyCollisionRadius; } }
        public float CollisionRadius { get { return m_CollisionRadius; } set { m_CollisionRadius = value; } }
        public bool ApplyFadeCharacter { get { return m_ApplyFadeCharacter; } }
        public bool FadeCharacter { get { return m_FadeCharacter; } set { m_FadeCharacter = value; } }
        public float StartFadeDistance { get { return m_StartFadeDistance; } set { m_StartFadeDistance = value; } }
        public float EndFadeDistance { get { return m_EndFadeDistance; } set { m_EndFadeDistance = value; } }

        public bool ApplyTargetLock { get { return m_ApplyTargetLock; } }
        public bool UseTargetLock { get { return m_UseTargetLock; } set { m_UseTargetLock = value; } }
        public float TargetLockSpeed { get { return m_TargetLockSpeed; } set { m_TargetLockSpeed = value; } }
        public float BreakForce { get { return m_BreakForce; } set { m_BreakForce = value; } }
        public bool UseHumanoidTargetLock { get { return m_UseHumanoidTargetLock; } set { m_UseHumanoidTargetLock = value; } }
        public HumanBodyBones HumanoidTargetLockBone { get { return m_HumanoidTargetLockBone; } set { m_HumanoidTargetLockBone = value; } }

        public bool ApplyRecoil { get { return m_ApplyRecoil; } }
        public float RecoilSpring { get { return m_RecoilSpring; } set { m_RecoilSpring = value; } }
        public float RecoilDampening { get { return m_RecoilDampening; } set { m_RecoilDampening = value; } }

        public bool ApplyObstructionCheck { get { return m_ApplyObstructionCheck; } }
        public bool ObstructionCheck { get { return m_ObstructionCheck; } set { m_ObstructionCheck = value; } }
        public bool ApplyStaticHeight { get { return m_ApplyStaticHeight; } }
        public bool StaticHeight { get { return m_StaticHeight; } set { m_StaticHeight = value; } }
        public bool ApplyVerticalOffset { get { return m_ApplyVerticalOffset; } }
        public float VerticalOffset { get { return m_VerticalOffset; } set { m_VerticalOffset = value; } }
    }
}