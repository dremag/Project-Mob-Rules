using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks.Movement
{
    public abstract class NavMeshMovement : Movement
    {
        [Tooltip("The speed of the agent")]
        public SharedFloat speed = 10;
        [Tooltip("The angular speed of the agent")]
        public SharedFloat angularSpeed = 120;
        [Tooltip("The agent has arrived when they are less than the specified distance")]
        public SharedFloat arrivedDistance = 1;

        private NavMeshAgent navMeshAgent;

        public override void OnAwake()
        {
            // cache for quick lookup
            navMeshAgent = gameObject.GetComponent<NavMeshAgent>();
        }

        public override void OnStart()
        {
            navMeshAgent.speed = speed.Value;
            navMeshAgent.angularSpeed = angularSpeed.Value;
            navMeshAgent.Resume();
        }

        protected override bool SetDestination(Vector3 target)
        {
            if (navMeshAgent.destination == target) {
                return true;
            }
            if (navMeshAgent.SetDestination(target)) {
                return true;
            }
            return false;
        }

        protected override bool HasArrived()
        {
            var direction = (navMeshAgent.destination - transform.position);
            // Do not account for the y difference if it is close to zero.
            if (Mathf.Abs(direction.y) < 0.1f) {
                direction.y = 0;
            }
            return !navMeshAgent.pathPending && direction.magnitude <= arrivedDistance.Value;
        }

        protected bool SamplePosition(Vector3 position)
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(position, out hit, float.MaxValue, NavMesh.AllAreas);
        }

        protected override Vector3 Velocity()
        {
            return navMeshAgent.velocity;
        }

        public override void OnEnd()
        {
            navMeshAgent.Stop();
        }

        public override void OnReset()
        {
            speed = 10;
            angularSpeed = 120;
        }
    }
}