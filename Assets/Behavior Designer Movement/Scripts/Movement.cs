using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks.Movement
{
    public abstract class Movement : Action
    {
        protected abstract bool SetDestination(Vector3 target);

        protected abstract bool HasArrived();

        protected abstract Vector3 Velocity();
    }
}