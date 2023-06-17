namespace Stateless
{
    public partial class StateMachine<TState, TTrigger, TContext>
    {
        /// <summary>
        /// Describes an initial state transition.
        /// </summary>
        public class InitialTransition : Transition
        {
	        /// <summary>
	        /// Construct a transition.
	        /// </summary>
	        /// <param name="source">The state transitioned from.</param>
	        /// <param name="destination">The state transitioned to.</param>
	        /// <param name="trigger">The trigger that caused the transition.</param>
	        /// <param name="context"></param>
	        /// <param name="parameters">The optional trigger parameters</param>
	        public InitialTransition(TState source, TState destination, TTrigger trigger, TContext context, object[] parameters = null) : base(source, destination, trigger, context, parameters)
            {
            }
        }
        
        public virtual Transition CreateTransition(TState source, TState destination, TTrigger trigger, TContext context, object[] parameters = null)
        {
	        return new StateMachine<TState, TTrigger, TContext>.Transition(source, destination, trigger, context,
		        parameters);
        }

        /// <summary>
        /// Describes a state transition.
        /// </summary>
        public class Transition
        {
	        /// <summary>
	        /// Construct a transition.
	        /// </summary>
	        /// <param name="source">The state transitioned from.</param>
	        /// <param name="destination">The state transitioned to.</param>
	        /// <param name="trigger">The trigger that caused the transition.</param>
	        /// <param name="context"></param>
	        /// <param name="parameters">The optional trigger parameters</param>
	        public Transition(TState source, TState destination, TTrigger trigger, TContext context, object[] parameters = null)
            {
                Source = source;
                Destination = destination;
                Trigger = trigger;
                Context = context;
                Parameters = parameters ?? new object[0];
            }

	        /// <summary>
            /// The state transitioned from.
            /// </summary>
            public TState Source { get; }

            /// <summary>
            /// The state transitioned to.
            /// </summary>
            public TState Destination { get; }

            /// <summary>
            /// The trigger that caused the transition.
            /// </summary>
            public TTrigger Trigger { get; }
            
            /// <summary>
            /// 
            /// </summary>
            public TContext Context { get; }

            /// <summary>
            /// True if the transition is a re-entry, i.e. the identity transition.
            /// </summary>
            public bool IsReentry => Source.Equals(Destination);

            /// <summary>
            /// The trigger parameters
            /// </summary>
            /// <remarks>
            /// Never null. For a parameterless trigger the value will be an empty array.
            /// </remarks>
            public object[] Parameters { get; }
        }
    }
}
