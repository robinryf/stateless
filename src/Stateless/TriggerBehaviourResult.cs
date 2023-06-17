using System.Collections.Generic;

namespace Stateless
{
    public partial class StateMachine<TState, TTrigger, TContext>
	    where TContext : StateMachine<TState, TTrigger, TContext>.IStateMachineContext
    {
        internal class TriggerBehaviourResult
        {
            public TriggerBehaviourResult(TriggerBehaviour handler, ICollection<string> unmetGuardConditions)
            {
                Handler = handler;
                UnmetGuardConditions = unmetGuardConditions;
            }
            public TriggerBehaviour Handler { get; }
            public ICollection<string> UnmetGuardConditions { get; }
        }
    }
}
