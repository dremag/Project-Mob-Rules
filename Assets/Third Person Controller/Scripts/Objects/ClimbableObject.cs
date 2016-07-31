using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any object which allows the character to climb. This includes ladders, vines, and pipes.
    /// </summary>
    public class ClimbableObject : MonoBehaviour
    {
        // The type of object that this component has been added to
        public enum ClimbableType { Ladder, Vine, Pipe }
        // The direction that the character is moving
        public enum MoveType { None, Up, Down, HorizontalForward, HorizontalBackward }

        [Tooltip("The type of object that this component has been added to")]
        [SerializeField] protected ClimbableType m_ClimbableType;
        [Tooltip("Used by all: can the character mount and face the opposite side of the ClimbableObject?")]
        [SerializeField] protected bool m_CanReverseMount;
        [Tooltip("Used by all: the offset that the character should move to when starting to climb from the bottom. A value of -1 means no movement on that axis")]
        [SerializeField] protected Vector3 m_BottomMountOffset = new Vector3(-1, -1, -1);
        [Tooltip("Used by ladders and vines: the offset that the character should move to when starting to climb from the top. A value of -1 means no movement on that axis")]
        [SerializeField] protected Vector3 m_TopMountOffset = new Vector3(-1, -1, -1);
        [Tooltip("Used by ladders and vines: the offset that the character should end up at after done mounting")]
        [SerializeField] protected Vector3 m_TopMountCompleteOffset;
        [Tooltip("Used by ladders: the separation between ladder rungs")]
        [SerializeField] protected float m_RungSeparation;
        [Tooltip("Used by ladders: the number of rungs that are unuseable by the characters feet")]
        [SerializeField] protected int m_UnuseableTopRungs = 2;
        [Tooltip("Used by vines: the padding from the left and right sides of the box collider that prevent the character from moving too far horizontally")]
        [SerializeField] protected float m_HorizontalPadding = 0.5f;
        [Tooltip("Used by vines: the offset from the top of the box collider that the character should start dismounting from")]
        [SerializeField] protected float m_TopDismountOffset;
        [Tooltip("Used by vines and pipes: the offset from the bottom of the object that the character should start dismounting from")]
        [SerializeField] protected float m_BottomDismountOffset;
        [Tooltip("Used by pipes: the transforms of the positions that the character can start climbing from")]
        [SerializeField] protected Transform[] m_MountPositions;
        [Tooltip("Used by pipes: the horizontal offset from the object position to transition from a horizontal to vertical pipe")]
        [SerializeField] protected float m_HorizontalTransitionOffset;
        [Tooltip("Used by pipes: the vertical offset from the object position to transition from a vertical to horizontal pipe")]
        [SerializeField] protected float m_VerticalTransitionOffset;
        [Tooltip("Used by pipes: the amount of extra transition distance of the forward movement compared to the backward movement")]
        [SerializeField] protected float m_ExtraForwardDistance;

        // Exposed properties
        public ClimbableType Type { get { return m_ClimbableType; } }
        public bool CanReverseMount { get { return m_CanReverseMount; } }
        public float HorizontalPadding { get { return m_HorizontalPadding; } }
        public float RungSeparation { get { return m_RungSeparation; } }

        // Internal variables
        private int m_RungCount;
        private int m_RungIndex;
        private float m_Bottom;
        private Vector3 m_Size;

        // Component references
        private Transform m_Transform;
        private Collider m_Collider;
        private Transform m_CharacterTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Collider = GetComponent<Collider>();

            m_Size = m_Transform.InverseTransformVector(m_Collider.bounds.size);
            m_Size.x = Mathf.Abs(m_Size.x);
            m_Size.z = Mathf.Abs(m_Size.z);
            m_Bottom = m_Transform.position.y + (m_Collider.bounds.center - m_Transform.position).y - m_Size.y / 2;
            // Subtract two because there are no rungs on the top or bottom.
            m_RungCount = Mathf.RoundToInt(m_Size.y / m_RungSeparation - 2);
        }

        /// <summary>
        /// The character is starting to mount on the ClimbableObject.
        /// </summary>
        /// <param name="characterTransform">The transform of the character.</param>
        public void Mount(Transform characterTransform)
        {
            m_CharacterTransform = characterTransform;
            // If mounting on a ladder then determine which rung index the character will be starting on.
            if (m_ClimbableType == ClimbableType.Ladder) {
                m_RungIndex = TopMount() ? (m_RungCount - m_UnuseableTopRungs - 1) : 0;
            }
        }

        /// <summary>
        /// Is the character mounting on the top of the ClimbableObject?
        /// </summary>
        /// <returns>True if the character is mounting on the top of the ClimbableObject.</returns>
        public bool TopMount()
        {
            return m_CharacterTransform.position.y > m_Transform.position.y + (m_Collider.bounds.center - m_Transform.position).y;
        }

        /// <summary>
        /// Returns the position that the character should start mounting from.
        /// </summary>
        /// <param name="rawOffset">Should the raw mount offset be returned?</param>
        /// <returns>The position that the character should start mounting from.</returns>
        public Vector3 MountPosition(bool rawOffset)
        {
            var offset = TopMount() ? m_TopMountOffset : m_BottomMountOffset;
            if (rawOffset) {
                if (offset.x == -1) {
                    offset.x = 0;
                }
                if (offset.y == -1) {
                    offset.y = 0;
                }
                if (offset.z == -1) {
                    offset.z = 0;
                }
                return offset;
            }
            // Find the closest mount position to the character.
            var closestTransform = m_Transform;
            // A pipe can have multiple mounting positions. Determine which position is closest.
            if (m_ClimbableType == ClimbableType.Pipe) {
                var distance = Vector3.SqrMagnitude(m_Transform.position - m_CharacterTransform.position);
                var localDistance = 0f;
                for (int i = 0; i < m_MountPositions.Length; ++i) {
                    if ((localDistance = Vector3.SqrMagnitude(m_MountPositions[i].position - m_CharacterTransform.position)) < distance) {
                        closestTransform = m_MountPositions[i];
                        distance = localDistance;
                    }
                }
            }
            // The closest mount position has been found. Determine the mount offset by first converting to local coordinates.
            var position = closestTransform.InverseTransformPoint(m_CharacterTransform.position);
            if (offset.x != -1) {
                position.x = offset.x;
            }
            if (offset.y != -1) {
                position.y = offset.y;
            }
            if (offset.z != -1) {
                // If the object can be mounted in reverse then set a position on the same side as the character.
                position.z = offset.z * ((m_CanReverseMount && Mathf.Sign(m_Transform.InverseTransformPoint(m_CharacterTransform.position).z) == -1) ? -1 : 1);
            }
            // The local mount position has been found. Return the world position.
            return closestTransform.TransformPoint(position);
        }

        /// <summary>
        /// Returns the position that the character will complete its top mount at.
        /// </summary>
        /// <returns>The position that the character will complete its top mount at.</returns>
        public Vector3 TopMountCompletePosition()
        {
            var offset = m_TopMountCompleteOffset;
            // The character can mount of any local x position on a vine.
            if (m_ClimbableType == ClimbableType.Vine) {
                var position = m_Transform.InverseTransformPoint(m_CharacterTransform.position);
                offset.x = position.x;
            }
            return m_Transform.TransformPoint(offset);
        }

        /// <summary>
        /// Used by ladder: the character has moved up or down a rung.
        /// </summary>
        /// <param name="moveType">The type of movement.</param>
        public void Move(MoveType moveType)
        {
            if (m_ClimbableType == ClimbableType.Ladder) {
                m_RungIndex += moveType == MoveType.Up ? 1 : -1;
            }
        }

        /// <summary>
        /// Used by pipes: should the character start transitioning between a horizontal and vertical climb position?
        /// </summary>
        /// <param name="moveType">The type of movement</param>
        /// <param name="vertical">Is the character currently on the vertical pipe section?</param>
        /// <param name="right">Did the character start on the right side of the pipe?</param>
        /// <returns>Should the character start a pipe transition?</returns>
        public bool ShouldStartPipeTransition(MoveType moveType, bool vertical, bool right)
        {
            if (moveType == MoveType.None) {
                return false;
            }

            if (m_ClimbableType == ClimbableType.Pipe) {
                if (vertical) {
                    // Start trasitioning if the character is currently vertical and is moving up above the transition offset.
                    if (moveType == MoveType.Up && m_CharacterTransform.position.y > m_Transform.position.y - m_VerticalTransitionOffset) {
                        return true;
                    }
                } else {
                    // A horizontal to vertical transition takes more work then the vertical to horizontal. The character should transition back onto the vertical section
                    // on the same side of the pipe as they started on.
                    var forwardOffset = Vector3.zero;
                    forwardOffset.x = moveType == MoveType.HorizontalForward ? m_ExtraForwardDistance : 0;
                    var characterOffset = m_Transform.InverseTransformPoint(m_CharacterTransform.position + m_Transform.rotation * forwardOffset);
                    if (right) {
                        if (moveType == MoveType.HorizontalForward) {
                            // If the character came from the right side and is moving forward then the character can start to transition as soon as the local offset is greater then the
                            // transition offset.
                            return characterOffset.x <= -m_HorizontalTransitionOffset;
                        } else if (moveType == MoveType.HorizontalBackward) {
                            // If the character came from the right side and is moving backward then the character can start to transition as soon as the local offset is less then the
                            // transition offset.
                            return characterOffset.x >= m_HorizontalTransitionOffset;
                        }
                    } else {
                        if (moveType == MoveType.HorizontalForward) {
                            // If the character came from the left side and is moving forward then the character can start to transition as soon as the local offset is less then the
                            // transition offset.
                            return characterOffset.x >= m_HorizontalTransitionOffset;
                        } else if (moveType == MoveType.HorizontalBackward) {
                            // If the character came from the left side and is moving backward then the character can start to transition as soon as the local offset is greater then the
                            // transition offset.
                            return characterOffset.x <= -m_HorizontalTransitionOffset;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the closest transition to the character. This is used by the character to ensure the character doesn't overshoot the vertical pipe when
        /// transition from horizontal to vertical.
        /// </summary>
        /// <returns>The target transform.</returns>
        public Transform HorizontalVerticalTransitionTargetTransform()
        {
            // Find the closest mount position to the character.
            var closestTransform = m_Transform;
            // A pipe can have multiple mounting positions. Determine which position is closest.
            var distance = Vector3.SqrMagnitude(m_Transform.position - m_CharacterTransform.position);
            var localDistance = 0f;
            for (int i = 0; i < m_MountPositions.Length; ++i) {
                if ((localDistance = Vector3.SqrMagnitude(m_MountPositions[i].position - m_CharacterTransform.position)) < distance) {
                    closestTransform = m_MountPositions[i];
                    distance = localDistance;
                }
            }
            return closestTransform;
        }

        /// <summary>
        /// The character is starting to transition from a vertical to horizontal pipe section. Should the right side transition animation play?
        /// </summary>
        /// <returns></returns>
        public bool ShouldTransitionRight()
        {
            return m_CharacterTransform.InverseTransformPoint(m_Transform.position).x > 0;
        }

        /// <summary>
        /// The character is starting to move on a horizontal pipe. Is the character on the right side of the pipe?
        /// </summary>
        /// <returns>True if the character is on the right side.</returns>
        public bool OnRightSide()
        {
            return m_Transform.InverseTransformPoint(m_CharacterTransform.position).x > 0;
        }

        /// <summary>
        /// Can the character dismount?
        /// </summary>
        /// <param name="moveType">The type of movement.</param>
        /// <param name="stateChange">Has the character changed climbing states?</param>
        /// <returns>True if the character can dismount.</returns>
        public bool CanDismount(MoveType moveType, bool stateChange)
        {
            // Don't dismount if there is no movement.
            if (moveType == MoveType.None) {
                return false;
            }

            // Each ClimbableType has different criteria for determining if the character should dismount.
            if (m_ClimbableType == ClimbableType.Ladder) {
                // The character can only dismount when changing states. This prevents the character from tying to dismount in between rungs.
                if (stateChange) {
                    // Dismount if moving down and on the bottom rung.
                    if (moveType == MoveType.Down && m_RungIndex == 0) {
                        return true;
                    }
                    // Dismount if moving up and on the top rung.
                    if (moveType == MoveType.Up && m_RungIndex == (m_RungCount - m_UnuseableTopRungs - 1)) {
                        return true;
                    }
                }
            } else if (m_ClimbableType == ClimbableType.Vine) {
                if (moveType == MoveType.Down) {
                    // Dismount if moving down and at the bottom.
                    return m_CharacterTransform.position.y < m_Bottom + m_BottomDismountOffset;
                } else if (moveType == MoveType.Up) {
                    // Dismount if moving up and at the top.
                    return m_CharacterTransform.position.y > m_Bottom + m_Size.y - m_TopDismountOffset;
                }
            } else if (m_ClimbableType == ClimbableType.Pipe) {
                if (moveType == MoveType.Down) {
                    // Dismount if moving down and at the bottom.
                    return m_CharacterTransform.position.y < m_Transform.position.y - m_BottomDismountOffset;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw editor gizmos to show the mount, dismount, and transition positions.
        /// Green: Mount
        /// Yellow: Transition
        /// Red: Dismount
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // The climbable object must have a collider.
            Collider collider = null;
            if ((collider = GetComponent<Collider>()) == null) {
                return;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            var localSize = transform.InverseTransformVector(collider.bounds.size);
            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);
            var mountOffset = Vector3.zero;
            var dismountOffset = Vector3.zero; 
            var size = Vector3.zero;
            var sizeOffset = Vector3.zero;
            var red = Color.red;
            var yellow = Color.yellow;
            var green = Color.green;
            var blue = Color.blue;
            var cyan = Color.cyan;
            red.a = yellow.a = green.a = blue.a = cyan.a = 0.5f;
            switch (m_ClimbableType) {
                case ClimbableType.Ladder:
                    // Draw the bottom mount position.
                    Gizmos.color = green;
                    mountOffset = GizmosMountOffset(m_BottomMountOffset);
                    size.Set(localSize.x, 0.25f, Mathf.Abs(mountOffset.z * (m_CanReverseMount ? 2 : 1)));
                    sizeOffset.Set(0, -size.y / 2, size.z / 2);
                    Gizmos.DrawCube(mountOffset - sizeOffset, size);

                    // Draw the bottom dismount position.
                    Gizmos.color = red;
                    dismountOffset.Set(0, -localSize.y / 2 + m_RungSeparation / 2, mountOffset.z);
                    size.y = 0.1f;
                    size.z = mountOffset.z;
                    sizeOffset.Set(0, size.y / 2, size.z / 2);
                    Gizmos.DrawCube(dismountOffset - sizeOffset, size);

                    // Draw the top dismount position.
                    dismountOffset.Set(0, localSize.y / 2 - (m_UnuseableTopRungs * m_RungSeparation), mountOffset.z);
                    sizeOffset.Set(0, -size.y / 2, size.z / 2);
                    Gizmos.DrawCube(dismountOffset - sizeOffset, size);

                    // Draw the top mount complete position
                    Gizmos.color = cyan;
                    sizeOffset.Set(0, size.y / 2, size.z / 2);
                    Gizmos.DrawCube(m_TopMountCompleteOffset - sizeOffset, size);

                    // Draw the top mount position.
                    Gizmos.color = green;
                    mountOffset = GizmosMountOffset(m_TopMountOffset);
                    mountOffset.y = localSize.y / 2;
                    size.Set(localSize.x, 0.25f, Mathf.Abs(mountOffset.z * (m_CanReverseMount ? 2 : 1)));
                    sizeOffset.Set(0, 0.25f, -size.z / 2);
                    Gizmos.DrawCube(mountOffset - sizeOffset, size);

                    break;
                case ClimbableType.Pipe:
                    // A pipe will have multiple colliders. Determine the correct horizontal and vertical collider.
                    BoxCollider horizontalBoxCollider = null;
                    BoxCollider verticalBoxCollider = null;
                    var boxColliders = GetComponents<BoxCollider>();
                    for (int i = 0; i < boxColliders.Length; ++i) {
                        if (boxColliders[i].size.x >= localSize.x) {
                            horizontalBoxCollider = boxColliders[i];
                        } else {
                            verticalBoxCollider = boxColliders[i];
                        }
                    }
                    if (verticalBoxCollider == null || horizontalBoxCollider == null) {
                        return;
                    }

                    for (int i = 0; i < m_MountPositions.Length; ++i) {
                        // Draw the bottom mount position.
                        Gizmos.color = green;
                        Gizmos.matrix = m_MountPositions[i].localToWorldMatrix;
                        mountOffset = GizmosMountOffset(m_BottomMountOffset);
                        size.Set(verticalBoxCollider.size.x, 0.25f, mountOffset.z * (m_CanReverseMount ? 2 : 1));
                        sizeOffset.Set(0, -size.y / 2, size.z / 2);
                        Gizmos.DrawCube(mountOffset - sizeOffset, size);

                        // Draw the vertical to horizontal transition.
                        Gizmos.color = yellow;
                        Gizmos.matrix = transform.localToWorldMatrix;
                        var localMountPosition = transform.InverseTransformPoint(m_MountPositions[i].position);
                        localMountPosition.y = -m_VerticalTransitionOffset;
                        sizeOffset.Set(0, -size.y / 2, 0);
                        Gizmos.DrawCube(localMountPosition - sizeOffset, size);

                        // Draw the bottom dismount position.
                        Gizmos.color = red;
                        localMountPosition.y = -m_BottomDismountOffset;
                        Gizmos.DrawCube(localMountPosition - sizeOffset, size);
                    }

                    // Draw the horizontal to vertical transition.
                    Gizmos.color = yellow;
                    for (int i = 0; i < 2; ++i) {
                        var transitionPosition = Vector3.zero;
                        transitionPosition.x = m_HorizontalTransitionOffset * (i == 0 ? 1 : -1);
                        size.Set(verticalBoxCollider.size.x, 0.25f, horizontalBoxCollider.size.z);
                        sizeOffset.Set(0, size.y / 2, 0);
                        Gizmos.DrawCube(transitionPosition - sizeOffset, size);
                    }

                    break;
                case ClimbableType.Vine:
                    // Draw the bottom mount position.
                    Gizmos.color = green;
                    mountOffset = GizmosMountOffset(m_BottomMountOffset);
                    mountOffset.y = -localSize.y / 2;
                    size.Set(localSize.x, 0.25f, mountOffset.z * (m_CanReverseMount ? 2 : 1));
                    sizeOffset.Set(0, -size.y / 2, size.z / 2);
                    Gizmos.DrawCube(mountOffset - sizeOffset, size);

                    // Draw the bottom dismount position.
                    Gizmos.color = red;
                    dismountOffset.Set(0, -localSize.y / 2 + m_BottomDismountOffset, mountOffset.z);
                    size.y = 0.1f;
                    size.z = mountOffset.z;
                    Gizmos.DrawCube(dismountOffset - sizeOffset, size);

                    // Draw the top dismount position.
                    dismountOffset.Set(0, localSize.y / 2 - m_TopDismountOffset, mountOffset.z);
                    sizeOffset.Set(0, size.y / 2, size.z / 2);
                    Gizmos.DrawCube(dismountOffset - sizeOffset, size);

                    // Draw the top mount complete position
                    Gizmos.color = cyan;
                    size.y = 0.1f;
                    sizeOffset.Set(0, size.y / 2, size.z / 2);
                    Gizmos.DrawCube(m_TopMountCompleteOffset - sizeOffset, size);

                    // Draw the top mount position.
                    Gizmos.color = green;
                    mountOffset = GizmosMountOffset(m_TopMountOffset);
                    mountOffset.y = localSize.y / 2;
                    size.Set(localSize.x, 0.25f, Mathf.Abs(mountOffset.z * (m_CanReverseMount ? 2 : 1)));
                    sizeOffset.Set(0, size.y / 2, -size.z / 2);
                    Gizmos.DrawCube(mountOffset - sizeOffset, size);

                    break;
            }

            // Draw the character position if the game is running.
            if (m_CharacterTransform != null) {
                Gizmos.matrix = m_CharacterTransform.localToWorldMatrix;
                Gizmos.color = blue;
                var position = Vector3.zero;
                if (m_ClimbableType == ClimbableType.Pipe) {
                    position.x = m_ExtraForwardDistance;
                }
                position.z = 0.3f;
                size.Set(0.05f, 0.05f, 0.3f);
                Gizmos.DrawCube(position, size);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        /// <summary>
        /// Determine the mount offset for editor gizmos positioning.
        /// </summary>
        /// <param name="mountOffset">The unmodified mount offset.</param>
        /// <returns>The gizmos mount offset.</returns>
        private Vector3 GizmosMountOffset(Vector3 mountOffset)
        {
            var relativeMountOffset = Vector3.zero;
            if (mountOffset.x != -1) {
                relativeMountOffset.x = mountOffset.x;
            }
            if (mountOffset.y != -1) {
                relativeMountOffset.y = mountOffset.y;
            }
            if (mountOffset.z != -1) {
                relativeMountOffset.z = mountOffset.z;
            }
            return relativeMountOffset;
        }
#endif
    }
}