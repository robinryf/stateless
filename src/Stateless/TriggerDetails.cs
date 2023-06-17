using System.Collections.Generic;

namespace Stateless
{
    /// <summary>
    /// Represents a trigger with details of any configured trigger parameters.
    /// </summary>
    public sealed class TriggerDetails<TState, TTrigger, TContext>
	    where TContext : StateMachine<TState, TTrigger, TContext>.IStateMachineContext
    {
        /// <summary>
        /// Creates a new instance of <see cref="TriggerDetails{TState, TTrigger, TContext}"/>.
        /// </summary>
        /// <param name="trigger">The trigger.</param>
        /// <param name="triggerConfiguration">The trigger configurations dictionary.</param>
        internal TriggerDetails(TTrigger trigger, IDictionary<TTrigger, StateMachine<TState, TTrigger, TContext>.TriggerWithParameters> triggerConfiguration)
        {
            Trigger = trigger;
            HasParameters = triggerConfiguration.ContainsKey(trigger);
            Parameters = HasParameters ? triggerConfiguration[trigger] : null;
        }

        /// <summary>
        /// Gets the trigger.
        /// </summary>
        public TTrigger Trigger { get; }

        /// <summary>
        /// Gets a value indicating whether the trigger has been configured with parameters.
        /// </summary>
        public bool HasParameters { get; }

        /// <summary>
        /// When <see cref="HasParameters"/> is <code>true</code>, returns the parameters required by 
        /// this trigger; otherwise, returns <code>null</code>.
        /// </summary>
        public StateMachine<TState, TTrigger, TContext>.TriggerWithParameters Parameters { get; }
    }
}
