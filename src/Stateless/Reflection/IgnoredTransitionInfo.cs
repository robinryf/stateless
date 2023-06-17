using System.Collections.Generic;
using System.Linq;

namespace Stateless.Reflection
{
    /// <summary>
    /// Describes a trigger that is "ignored" (stays in the same state)
    /// </summary>
    public class IgnoredTransitionInfo : TransitionInfo
    {
        internal static IgnoredTransitionInfo Create<TState, TTrigger, TContext>(StateMachine<TState, TTrigger, TContext>.IgnoredTriggerBehaviour behaviour) where TContext : StateMachine<TState, TTrigger, TContext>.IStateMachineContext
        {
            var transition = new IgnoredTransitionInfo
            {
                Trigger = new TriggerInfo(behaviour.Trigger),
                GuardConditionsMethodDescriptions = behaviour.Guard == null
                    ? new List<InvocationInfo>() : behaviour.Guard.Conditions.Select(c => c.MethodDescription)
            };

            return transition;
        }

        private IgnoredTransitionInfo() { }
    }
}