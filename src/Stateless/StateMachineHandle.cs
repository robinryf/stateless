using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.Reflection;

namespace Stateless
{
	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TTrigger"></typeparam>
	/// <typeparam name="TContext"></typeparam>
	public partial class StateMachine<TState, TTrigger, TContext>
	{
		/// <summary>
		/// 
		/// </summary>
		public class StateMachineHandle
		{
			/// <summary>
			/// 
			/// </summary>
			public TContext Context { get; private set; }

			private TState _initialState;
			
			private StateMachine<TState, TTrigger, TContext> _machine;

			/// <summary>
			/// 
			/// </summary>
			/// <param name="context"></param>
			/// <param name="machine"></param>
			internal StateMachineHandle(TContext context, TState initialState, StateMachine<TState, TTrigger, TContext> machine)
			{
				Context = context;
				Context.State = _initialState;
				_machine = machine;
				_initialState = initialState;
			}
	        
			/// <summary>
        /// The currently-permissible trigger values.
        /// </summary>
        public IEnumerable<TTrigger> PermittedTriggers
        {
            get
            {
                return GetPermittedTriggers();
            }
        }

        /// <summary>
        /// The currently-permissible trigger values.
        /// </summary>
        public IEnumerable<TTrigger> GetPermittedTriggers(params object[] args)
        {
            return CurrentRepresentation.GetPermittedTriggers(args);
        }

        /// <summary>
        /// Gets the currently-permissible triggers with any configured parameters.
        /// </summary>
        public IEnumerable<TriggerDetails<TState, TTrigger, TContext>> GetDetailedPermittedTriggers(params object[] args)
        {
            return CurrentRepresentation.GetPermittedTriggers(args)
                .Select(trigger => new TriggerDetails<TState, TTrigger, TContext>(trigger, _machine._triggerConfiguration));
        }

        StateRepresentation CurrentRepresentation
        {
            get
            {
                return _machine.GetRepresentation(Context.State);
            }
        }

        /// <summary>
        /// Provides an info object which exposes the states, transitions, and actions of this machine.
        /// </summary>
        public StateMachineInfo GetInfo()
        {
            var initialState = StateInfo.CreateStateInfo(new StateRepresentation(_initialState, _machine.RetainSynchronizationContext));

            var representations = _machine._stateConfiguration.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var behaviours = _machine._stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<TransitioningTriggerBehaviour>().Select(tb => tb.Destination))).ToList();
            behaviours.AddRange(_machine._stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<ReentryTriggerBehaviour>().Select(tb => tb.Destination))).ToList());

            var reachable = behaviours
                .Distinct()
                .Except(representations.Keys)
                .Select(underlying => new StateRepresentation(underlying, _machine.RetainSynchronizationContext))
                .ToArray();

            foreach (var representation in reachable)
                representations.Add(representation.UnderlyingState, representation);

            var info = representations.ToDictionary(kvp => kvp.Key, kvp => StateInfo.CreateStateInfo(kvp.Value));

            foreach (var state in info)
                StateInfo.AddRelationships(state.Value, representations[state.Key], k => info[k]);

            return new StateMachineInfo(info.Values, typeof(TState), typeof(TTrigger), initialState);
        }
        
        
        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// The target state is determined by the configuration of the current state.
        /// Actions associated with leaving the current state and entering the new one
        /// will be invoked.
        /// </summary>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire(TTrigger trigger)
        {
	        _machine.InternalFire(trigger, Context, new object[0]);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// The target state is determined by the configuration of the current state.
        /// Actions associated with leaving the current state and entering the new one
        /// will be invoked.
        /// </summary>
        /// <param name="trigger">The trigger to fire.</param>
        /// <param name="args">A variable-length parameters list containing arguments. </param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire(TriggerWithParameters trigger, params object[] args)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            _machine.InternalFire(trigger.Trigger, Context, args);
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// </summary>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <param name="argumentTypes">The argument types expected by the trigger.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters SetTriggerParameters(TTrigger trigger, params Type[] argumentTypes)
        {
            var configuration = new TriggerWithParameters(trigger, argumentTypes);
            _machine.SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// The target state is determined by the configuration of the current state.
        /// Actions associated with leaving the current state and entering the new one
        /// will be invoked.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">The trigger to fire.</param>
        /// <param name="arg0">The first argument.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0>(TriggerWithParameters<TArg0> trigger, TArg0 arg0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            _machine.InternalFire(trigger.Trigger, Context, arg0);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// The target state is determined by the configuration of the current state.
        /// Actions associated with leaving the current state and entering the new one
        /// will be invoked.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, TArg0 arg0, TArg1 arg1)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            _machine.InternalFire(trigger.Trigger, Context, arg0, arg1);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// The target state is determined by the configuration of the current state.
        /// Actions associated with leaving the current state and entering the new one
        /// will be invoked.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            _machine.InternalFire(trigger.Trigger, Context, arg0, arg1, arg2);
        }

        /// <summary>
        /// Activates current state. Actions associated with activating the current state
        /// will be invoked. The activation is idempotent and subsequent activation of the same current state
        /// will not lead to re-execution of activation callbacks.
        /// </summary>
        public void Activate()
        {
            var representativeState = _machine.GetRepresentation(Context.State);
            representativeState.Activate();
        }

        /// <summary>
        /// Deactivates current state. Actions associated with deactivating the current state
        /// will be invoked. The deactivation is idempotent and subsequent deactivation of the same current state
        /// will not lead to re-execution of deactivation callbacks.
        /// </summary>
        public void Deactivate()
        {
            var representativeState = _machine.GetRepresentation(Context.State);
            representativeState.Deactivate();
        }
        
        
        /// <summary>
        /// Determine if the state machine is in the supplied state.
        /// </summary>
        /// <param name="state">The state to test for.</param>
        /// <returns>True if the current state is equal to, or a substate of,
        /// the supplied state.</returns>
        public bool IsInState(TState state)
        {
	        return CurrentRepresentation.IsIncludedIn(state);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state.
        /// </summary>
        /// <param name="trigger">Trigger to test.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TTrigger trigger)
        {
	        return CurrentRepresentation.CanHandle(trigger);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state.
        /// </summary>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TTrigger trigger, out ICollection<string> unmetGuards)
        {
	        return CurrentRepresentation.CanHandle(trigger, new object[] { }, out unmetGuards);
        }

        /// <summary>
        /// A human-readable representation of the state machine.
        /// </summary>
        /// <returns>A description of the current state and permitted triggers.</returns>
        public override string ToString()
        {
	        return string.Format(
		        "StateMachine {{ State = {0}, PermittedTriggers = {{ {1} }}}}",
		        Context.State,
		        string.Join(", ", GetPermittedTriggers().Select(t => t.ToString()).ToArray()));
        }


		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public StateMachineHandle CreateHandle(TContext context, TState initialState)
		{
			return new StateMachineHandle(context, initialState, this);
		}
	}
}