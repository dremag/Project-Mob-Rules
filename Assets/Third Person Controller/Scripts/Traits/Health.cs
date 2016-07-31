using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Adds health and a shield to the object. The shield can regenerate, health cannot. Can optionally sp
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class Health : NetworkBehaviour
#else
    public class Health : MonoBehaviour
#endif
    {
        /// <summary>
        /// Allows specified GameObjects to take a different amount of damage.
        /// </summary>
        [System.Serializable]
        public class DamageMultiplier
        {
            [Tooltip("The GameObject which should take more damage")]
            [SerializeField] protected GameObject m_GameObject;
            [Tooltip("The damage multiplier if the body part is hit")]
            [SerializeField] protected float m_Multiplier = 1;

            // Exposed properties
            public GameObject GameObject { get { return m_GameObject; } }
            public float Multiplier { get { return m_Multiplier; } }
        }

        [Tooltip("Is the object invincible?")]
        [SerializeField] protected bool m_Invincible;
        [Tooltip("The maximum amount of health, can be 0")]
        [SerializeField] protected float m_MaxHealth = 100;
        [Tooltip("The maximum amount of shield, can be 0")]
        [SerializeField] protected float m_MaxShield;
        [Tooltip("If using a shield, the amount of time to initially wait before the shield regenerates")]
        [SerializeField] protected float m_ShieldRegenerativeInitialWait = 2;
        [Tooltip("If using a shield, the amount to regenerate every interval")]
        [SerializeField] protected float m_ShieldRegenerativeAmount = 0.1f;
        [Tooltip("If using a shield, the amount of time to wait before regenerating a small amount of shield")]
        [SerializeField] protected float m_ShieldRegenerativeWait = 0.2f;
        [Tooltip("The list of colliders that should apply a multiplier when damaged")]
        [SerializeField] protected DamageMultiplier[] m_DamageMultipliers;
        [Tooltip("Any object that should spawn when the object dies")]
        [SerializeField] protected GameObject[] m_SpawnedObjectsOnDeath;
        [Tooltip("Any object that should be destroyed when the object dies")]
        [SerializeField] protected GameObject[] m_DestroyedObjectsOnDeath;
        [Tooltip("Should the object be deactivated on death?")]
        [SerializeField] protected bool m_DeactivateOnDeath;
        [Tooltip("If DeactivateOnDeath is enabled, specify a delay for the object to be deactivated")]
        [SerializeField] protected float m_DeactivateOnDeathDelay;
        [Tooltip("The layer that the GameObject should switch to upon death")]
        [SerializeField] protected LayerMask m_DeathLayer;
        [Tooltip("The amount of time that the object is invincible after respawning")]
        [SerializeField] protected float m_TimeInvincibleAfterSpawn;

        // Exposed properties
        public bool Invincible { set { m_Invincible = value; } }
        public float MaxHealth { set { m_MaxHealth = value; } }
        public float MaxShield { set { m_MaxShield = value; } }
        public float CurrentHealth { get { return m_CurrentHealth; } }
        public float CurrentShield { get { return m_CurrentShield; } }
        public float ShieldRegenerativeAmount { set { m_ShieldRegenerativeAmount = value; } }

        // Internal variables
#if ENABLE_MULTIPLAYER
        [SyncVar(hook = "SetHealthAmount")] 
#endif
        private float m_CurrentHealth;
#if ENABLE_MULTIPLAYER
        [SyncVar(hook = "SetShieldAmount")]
#endif
        private float m_CurrentShield;
        private ScheduledEvent m_ShieldRegenerativeEvent;
        private float m_SpawnTime;
        private int m_AliveLayer;
        private Dictionary<GameObject, DamageMultiplier> m_DamageMultiplierMap;

        // SharedFields
        protected float SharedProperty_MaxHealth { get { return m_MaxHealth; } }
        protected float SharedProperty_MaxShield { get { return m_MaxShield; } }
        protected float SharedProperty_CurrentHealth { get { return m_CurrentHealth; } }
        protected float SharedProperty_CurrentShield { get { return m_CurrentShield; } }

        // Component references
        protected GameObject m_GameObject;
        protected Transform m_Transform;
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected virtual void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Rigidbody = GetComponent<Rigidbody>();

            SharedManager.Register(this);

            m_CurrentHealth = m_MaxHealth;
            m_CurrentShield = m_MaxShield;
            if (m_DamageMultipliers != null && m_DamageMultipliers.Length > 0) {
                m_DamageMultiplierMap = new Dictionary<GameObject, DamageMultiplier>();
                for (int i = 0; i < m_DamageMultipliers.Length; ++i) {
                    m_DamageMultiplierMap.Add(m_DamageMultipliers[i].GameObject, m_DamageMultipliers[i]);
                }
            }

            // Register for OnRespawn so the health and sheild can be reset.
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Deactivate the object immediately if the object is not alive when the client joins.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsAlive() && m_DeactivateOnDeath) {
                Deactivate();
            }
        }
#endif
        /// <summary>
        /// Sets the health amount and executes an event for those objects interested in changes.
        /// </summary>
        /// <param name="value">The new health amount.</param>
        private void SetHealthAmount(float value)
        {
            m_CurrentHealth = value;
            EventHandler.ExecuteEvent<float>(m_GameObject, "OnHealthAmountChange", m_CurrentHealth);
        }

        /// <summary>
        /// Sets the shield amount and executes an event for those objects interested in changes.
        /// </summary>
        /// <param name="value">The new shield amount.</param>
        private void SetShieldAmount(float value)
        {
            m_CurrentShield = value;
            EventHandler.ExecuteEvent<float>(m_GameObject, "OnHealthShieldAmountChange", m_CurrentShield);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        public void Damage(float amount, Vector3 position, Vector3 force)
        {
            Damage(amount, position, force, 0, null, null);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        public void Damage(float amount, Vector3 position, Vector3 force, float radius)
        {
            Damage(amount, position, force, radius, null, null);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        public void Damage(float amount, Vector3 position, Vector3 force, GameObject attacker)
        {
            Damage(amount, position, force, 0, attacker, null);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        public void Damage(float amount, Vector3 position, Vector3 force, float radius, GameObject attacker)
        {
            Damage(amount, position, force, radius, attacker, null);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        /// <param name="hitGameObject">The GameObject that was hit.</param>
        public void Damage(float amount, Vector3 position, Vector3 force, GameObject attacker, GameObject hitGameObject)
        {
            Damage(amount, position, force, 0, attacker, hitGameObject);
        }

        /// <summary>
        /// The object has been damaged. Call the corresponding server or client method.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        /// <param name="hitGameObject">The GameObject that was hit.</param>
        public virtual void Damage(float amount, Vector3 position, Vector3 force, float radius, GameObject attacker, GameObject hitGameObject)
        {
            // Don't take any damage if the object is invincible, already dead, or just spawned and is invincible for a small amount of time.
            if (m_Invincible || !IsAlive() || m_SpawnTime + m_TimeInvincibleAfterSpawn > Time.time) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcDamage(amount, position, force, radius, attacker, hitGameObject);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                DamageLocal(amount, position, force, radius, attacker, hitGameObject);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The object has been damaged on the client. Update the health and shield. Execute the OnDeath events if the health and shield is equal to zero.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        /// <param name="hitGameObject">The GameObject that was hit.</param>
        [ClientRpc]
        private void RpcDamage(float amount, Vector3 position, Vector3 force, float radius, GameObject attacker, GameObject hitGameObject)
        {
            DamageLocal(amount, position, force, radius, attacker, hitGameObject);
        }
#endif

        /// <summary>
        /// The object has been damaged. Update the health and shield. Execute the OnDeath events if the health and shield is equal to zero.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-exposive force will be used.</param>
        /// <param name="attacker">The GameObject that did the damage.</param>
        /// <param name="hitGameObject">The GameObject that was hit.</param>
        private void DamageLocal(float amount, Vector3 position, Vector3 force, float radius, GameObject attacker, GameObject hitGameObject)
        {
            // Add a multiplier if a particular GameObject was hit. Do not apply a multiplier if the damage is applied through a radius because multiple
            // GameObjects are hit.
            if (radius == 0 && hitGameObject != null) {
                DamageMultiplier damageMultiplier;
                if (m_DamageMultiplierMap != null && m_DamageMultiplierMap.TryGetValue(hitGameObject, out damageMultiplier)) {
                    amount *= damageMultiplier.Multiplier;
                }
            }

            // Apply the damage to the shield first because the shield can regenrate.
            if (m_CurrentShield > 0) {
                var shieldAmount = Mathf.Min(amount, m_CurrentShield);
                amount -= shieldAmount;
                SetShieldAmount(m_CurrentShield - shieldAmount);
            }

            // Decrement the health by remaining amount after the shield has taken damage.
            if (m_CurrentHealth > 0) {
                SetHealthAmount(m_CurrentHealth - Mathf.Min(amount, m_CurrentHealth));
            }

            // Apply a force to the hit rigidbody if the force is greater than 0.
            if (m_Rigidbody != null && !m_Rigidbody.isKinematic && force.sqrMagnitude > 0) {
                if (radius == 0) {
                    m_Rigidbody.AddForceAtPosition(force, position);
                } else {
                    m_Rigidbody.AddExplosionForce(force.magnitude, position, radius);
                }
            }

            // Let other interested objects know that the object took damage.
            EventHandler.ExecuteEvent<float, Vector3, Vector3, GameObject>(m_GameObject, "OnHealthDamageDetails", amount, position, force, attacker);

            // The shield should stop regenerating when the object is taking damage.
            Scheduler.Cancel(ref m_ShieldRegenerativeEvent);

            // The object is dead when there is no more health or shield.
            if (!IsAlive()) {
                Die(position, force, attacker);
            // Regenerate the shield if the unit is still alive after taking damage. The server should be the only object regenerating the shield if on the network.
#if ENABLE_MULTIPLAYER
            } else if (m_MaxShield > 0 && isServer) {
#else
            } else if (m_MaxShield > 0) {
#endif
                m_ShieldRegenerativeEvent = Scheduler.Schedule(m_ShieldRegenerativeInitialWait, RegenerateShield);
            }
        }

        /// <summary>
        /// Is the object currently alive?
        /// </summary>
        /// <returns>True if the object is currently alive.</returns>
        public bool IsAlive()
        {
            return m_CurrentHealth > float.Epsilon || m_CurrentShield > float.Epsilon;
        }

        /// <summary>
        /// Instantly kill the object.
        /// </summary>
        public void InstantDeath()
        {
            Damage(m_CurrentHealth + m_CurrentShield, Vector3.zero, Vector3.zero);
        }

        /// <summary>
        /// The object is no longer alive. Kill it. Call the corresponding server or client method.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        protected virtual void Die(Vector3 position, Vector3 force, GameObject attacker)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcDie(position, force, attacker);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                DieLocal(position, force, attacker);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The object is no longer alive on the client. Kill it.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        [ClientRpc]
        private void RpcDie(Vector3 position, Vector3 force, GameObject attacker)
        {
            DieLocal(position, force, attacker);
        }
#endif

        /// <summary>
        /// The object is no longer alive. Kill it.
        /// </summary>
        /// <param name="position">The position of the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        private void DieLocal(Vector3 position, Vector3 force, GameObject attacker)
        {
            // Notify those interested.
            EventHandler.ExecuteEvent(m_GameObject, "OnDeath");
            EventHandler.ExecuteEvent<Vector3, Vector3, GameObject>(m_GameObject, "OnDeathDetails", force, position, attacker);

            // Spawn any objects on death, such as an explosion if the object is an explosive barrell.
            for (int i = 0; i < m_SpawnedObjectsOnDeath.Length; ++i) {
                var spawnedObject = ObjectPool.Instantiate(m_SpawnedObjectsOnDeath[i], transform.position, transform.rotation);
                Explosion explosion;
                if ((explosion = spawnedObject.GetComponent<Explosion>()) != null) {
                    explosion.Originator = gameObject;
                }
            }

            // Destroy any objects on death. The objects will be placed back in the object pool if they were created within it otherwise the object will be destroyed.
            for (int i = 0; i < m_DestroyedObjectsOnDeath.Length; ++i) {
                if (ObjectPool.SpawnedWithPool(m_DestroyedObjectsOnDeath[i])) {
                    ObjectPool.Destroy(m_DestroyedObjectsOnDeath[i]);
                } else {
                    Object.Destroy(m_DestroyedObjectsOnDeath[i]);
                }
            }

            // Change the layer to a death layer.
            if (m_DeathLayer.value != -2) {
                m_AliveLayer = m_GameObject.layer;
                m_GameObject.layer = m_DeathLayer;
            }

            // Stop any scheduled events.
            Scheduler.Cancel(ref m_ShieldRegenerativeEvent);

            // Deactivate the object if requested.
            if (m_DeactivateOnDeath) {
                Scheduler.Schedule(m_DeactivateOnDeathDelay, Deactivate);
            }
        }

        /// <summary>
        /// Slowly regenerate a depleted shield. Keep regenerating until the shield is full again or the object takes more damage.
        /// </summary>
        private void RegenerateShield()
        {
            SetShieldAmount(Mathf.Min(m_CurrentShield + m_ShieldRegenerativeAmount, m_MaxShield));
            if (m_CurrentShield < m_MaxShield) {
                m_ShieldRegenerativeEvent = Scheduler.Schedule(m_ShieldRegenerativeWait, RegenerateShield);
            }
        }

        /// <summary>
        /// Adds amount to health and then to the shield if there is still an amount remaining. Will not go over MaxHealth or MaxShield.
        /// </summary>
        /// <param name="amount">The amount of health or shield to add.</param>
        public virtual void Heal(float amount)
        {
            // Contribute the amount of the health first.
            if (m_MaxHealth > 0 && m_CurrentHealth < m_MaxHealth) {
                var healthAmount = Mathf.Min(amount, m_MaxHealth - m_CurrentHealth);
                amount -= healthAmount;
                SetHealthAmount(m_CurrentHealth + healthAmount);
            }

            // Add any remaining amount to the shield.
            if (amount > 0 && m_MaxShield > 0 && m_CurrentShield < m_MaxShield) {
                var shieldAmount = Mathf.Min(amount, m_MaxShield - m_CurrentShield);
                SetShieldAmount(m_CurrentHealth + shieldAmount);
            }
        }

        /// <summary>
        /// The object doesn't have any health or shield left and should be deactivated.
        /// </summary>
        private void Deactivate()
        {
            Collider objCollider;
            if ((objCollider = GetComponent<Collider>()) != null) {
                LayerManager.RevertCollision(objCollider);
            }

            m_GameObject.SetActive(false);
        }

        /// <summary>
        /// The object has spawned again. Set the health and shield back to their starting values.
        /// </summary>
        private void OnRespawn()
        {
            SetHealthAmount(m_MaxHealth);
            SetShieldAmount(m_MaxShield);
            // Change the layer back to the alive layer.
            if (m_DeathLayer.value != -2) {
                m_GameObject.layer = m_AliveLayer;
            }
            m_SpawnTime = Time.time;
        }
    }
}