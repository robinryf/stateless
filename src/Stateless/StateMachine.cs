using Stateless.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stateless
{
    /// <summary>
    /// Enum for the different modes used when Fire-ing a trigger
    /// </summary>
    public enum FiringMode
    {
        /// <summary> Use immediate mode when the queuing of trigger events are not needed. Care must be taken when using this mode, as there is no run-to-completion guaranteed.</summary>
        Immediate,
        /// <summary> Use the queued Fire-ing mode when run-to-completion is required. This is the recommended mode.</summary>
        Queued
    }

    /// <summary>
    /// Models behaviour as transitions between a finite set of states.
    /// </summary>
    /// <typeparam name="TState">The type used to represent the states.</typeparam>
    /// <typeparam name="TTrigger">The type used to represent the triggers that cause state transitions.</typeparam>
    /// <typeparam name="TContext"></typeparam>
    public partial class StateMachine<TState, TTrigger, TContext>
		where TContext : StateMachine<TState, TTrigger, TContext>.IStateMachineContext
    {
        private readonly IDictionary<TState, StateRepresentation> _stateConfiguration = new Dictionary<TState, StateRepresentation>();
        private readonly IDictionary<TTrigger, TriggerWithParameters> _triggerConfiguration = new Dictionary<TTrigger, TriggerWithParameters>();
        private UnhandledTriggerAction _unhandledTriggerAction;
        private readonly OnTransitionedEvent _onTransitionedEvent;
        private readonly OnTransitionedEvent _onTransitionCompletedEvent;
        protected FiringMode _firingMode;

        public interface IStateMachineContext
        {
	        TState State { get; set; }
        }
        
        private class QueuedTrigger
        {
            public TTrigger Trigger { get; set; }
            public TContext Context { get; set; }
            public object[] Args { get; set; }
        }

        private readonly Queue<QueuedTrigger> _eventQueue = new Queue<QueuedTrigger>();
        private bool _firing;


        /// <summary>
        /// For certain situations, it is essential that the SynchronizationContext is retained for all delegate calls.
        /// </summary>
        public bool RetainSynchronizationContext { get; set; } = false;

        /// <summary>
        /// Default constructor
        /// </summary>
        public StateMachine()
        {
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync(DefaultUnhandledTriggerAction);
            _onTransitionedEvent = new OnTransitionedEvent();
            _onTransitionCompletedEvent = new OnTransitionedEvent();
        }


        /// <summary>
        /// The currently-permissible trigger values.
        /// </summary>
        protected IEnumerable<TTrigger> GetPermittedTriggers(TState state, params object[] args)
        {
            return GetRepresentation(state).GetPermittedTriggers(args);
        }

        /// <summary>
        /// Gets the currently-permissible triggers with any configured parameters.
        /// </summary>
        public IEnumerable<TriggerDetails<TState, TTrigger, TContext>> GetDetailedPermittedTriggers(TState state, params object[] args)
        {
            return GetRepresentation(state).GetPermittedTriggers(args)
                .Select(trigger => new TriggerDetails<TState, TTrigger, TContext>(trigger, _triggerConfiguration));
        }
        /// <summary>
        /// Provides an info object which exposes the states, transitions, and actions of this machine.
        /// </summary>
        public StateMachineInfo GetInfo(TState _initialState)
        {
            var initialState = StateInfo.CreateStateInfo(new StateRepresentation(_initialState, RetainSynchronizationContext));

            var representations = _stateConfiguration.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var behaviours = _stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<TransitioningTriggerBehaviour>().Select(tb => tb.Destination))).ToList();
            behaviours.AddRange(_stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<ReentryTriggerBehaviour>().Select(tb => tb.Destination))).ToList());

            var reachable = behaviours
                .Distinct()
                .Except(representations.Keys)
                .Select(underlying => new StateRepresentation(underlying, RetainSynchronizationContext))
                .ToArray();

            foreach (var representation in reachable)
                representations.Add(representation.UnderlyingState, representation);

            var info = representations.ToDictionary(kvp => kvp.Key, kvp => StateInfo.CreateStateInfo(kvp.Value));

            foreach (var state in info)
                StateInfo.AddRelationships(state.Value, representations[state.Key], k => info[k]);

            return new StateMachineInfo(info.Values, typeof(TState), typeof(TTrigger), initialState);
        }
        private StateRepresentation GetRepresentation(TState state)
        {
            if (!_stateConfiguration.TryGetValue(state, out StateRepresentation result))
            {
                result = new StateRepresentation(state, RetainSynchronizationContext);
                _stateConfiguration.Add(state, result);
            }

            return result;
        }

        /// <summary>
        /// Begin configuration of the entry/exit actions and allowed transitions
        /// when the state machine is in a particular state.
        /// </summary>
        /// <param name="state">The state to configure.</param>
        /// <returns>A configuration object through which the state can be configured.</returns>
        public virtual StateConfiguration Configure(TState state)
        {
            return new StateConfiguration(this, GetRepresentation(state), GetRepresentation);
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
            SaveTriggerConfiguration(configuration);
            return configuration;
        }
        /// <summary>
        /// Activates current state. Actions associated with activating the current state
        /// will be invoked. The activation is idempotent and subsequent activation of the same current state
        /// will not lead to re-execution of activation callbacks.
        /// </summary>
        public void Activate(TState currentState)
        {
            var representativeState = GetRepresentation(currentState);
            representativeState.Activate();
        }

        /// <summary>
        /// Deactivates current state. Actions associated with deactivating the current state
        /// will be invoked. The deactivation is idempotent and subsequent deactivation of the same current state
        /// will not lead to re-execution of deactivation callbacks.
        /// </summary>
        public void Deactivate(TState currentState)
        {
            var representativeState = GetRepresentation(currentState);
            representativeState.Deactivate();
        }
        

        /// <summary>
        /// Determine how to Fire the trigger
        /// </summary>
        /// <param name="trigger">The trigger. </param>
        /// <param name="context"></param>
        /// <param name="args">A variable-length parameters list containing arguments. </param>
        protected void InternalFire(TTrigger trigger, TContext context, params object[] args)
        {
            switch (_firingMode)
            {
                case FiringMode.Immediate:
                    InternalFireOne(trigger, context, args);
                    break;
                case FiringMode.Queued:
                    InternalFireQueued(trigger, context, args);
                    break;
                default:
                    // If something is completely messed up we let the user know ;-)
                    throw new InvalidOperationException("The firing mode has not been configured!");
            }
        }

        /// <summary>
        /// Queue events and then fire in order.
        /// If only one event is queued, this behaves identically to the non-queued version.
        /// </summary>
        /// <param name="trigger">  The trigger. </param>
        /// <param name="context"></param>
        /// <param name="args">     A variable-length parameters list containing arguments. </param>
        private void InternalFireQueued(TTrigger trigger, TContext context, params object[] args)
        {
            // Add trigger to queue
            _eventQueue.Enqueue(new QueuedTrigger { Trigger = trigger, Context = context, Args = args });

            // If a trigger is already being handled then the trigger will be queued (FIFO) and processed later.
            if (_firing)
            {
                return;
            }

            try
            {
                _firing = true;

                // Empty queue for triggers
                while (_eventQueue.Any())
                {
                    var queuedEvent = _eventQueue.Dequeue();
                    InternalFireOne(queuedEvent.Trigger, queuedEvent.Context, queuedEvent.Args);
                }
            }
            finally
            {
                _firing = false;
            }
        }

        /// <summary>
        /// This method handles the execution of a trigger handler. It finds a
        /// handle, then updates the current state information.
        /// </summary>
        /// <param name="trigger"></param>
        /// <param name="context"></param>
        /// <param name="args"></param>
        void InternalFireOne(TTrigger trigger, TContext context, params object[] args)
        {
            // If this is a trigger with parameters, we must validate the parameter(s)
            if (_triggerConfiguration.TryGetValue(trigger, out TriggerWithParameters configuration))
                configuration.ValidateParameters(args);

            var source = context.State;
            var representativeState = GetRepresentation(source);

            // Try to find a trigger handler, either in the current state or a super state.
            if (!representativeState.TryFindHandler(trigger, args, out TriggerBehaviourResult result))
            {
                _unhandledTriggerAction.Execute(representativeState.UnderlyingState, trigger, result?.UnmetGuardConditions);
                return;
            }

            switch (result.Handler)
            {
                // Check if this trigger should be ignored
                case IgnoredTriggerBehaviour _:
                    return;
                // Handle special case, re-entry in superstate
                // Check if it is an internal transition, or a transition from one state to another.
                case ReentryTriggerBehaviour handler:
                {
                    // Handle transition, and set new state
                    var transition = CreateTransition(source, handler.Destination, trigger, context, args);
                    HandleReentryTrigger(args, representativeState, transition);
                    break;
                }
                case DynamicTriggerBehaviour _ when result.Handler.ResultsInTransitionFrom(source, args, out var destination):
                case TransitioningTriggerBehaviour _ when result.Handler.ResultsInTransitionFrom(source, args, out destination):
                {
                    // Handle transition, and set new state
                    var transition = CreateTransition(source, destination, trigger, context, args);
                    HandleTransitioningTrigger(args, representativeState, transition);

                    break;
                }
                case InternalTriggerBehaviour _:
                {
                    // Internal transitions does not update the current state, but must execute the associated action.
                    var transition = CreateTransition(source, source, trigger, context, args);
                    GetRepresentation(context.State).InternalAction(transition, args);
                    break;
                }
                default:
                    throw new InvalidOperationException("State machine configuration incorrect, no handler for trigger.");
            }
        }

        private void HandleReentryTrigger(object[] args, StateRepresentation representativeState, Transition transition)
        {
            StateRepresentation representation;
            transition = representativeState.Exit(transition);
            var newRepresentation = GetRepresentation(transition.Destination);

            if (!transition.Source.Equals(transition.Destination))
            {
                // Then Exit the final superstate
                transition = CreateTransition(transition.Destination, transition.Destination, transition.Trigger, transition.Context, args);
                newRepresentation.Exit(transition);

                _onTransitionedEvent.Invoke(transition);
                representation = EnterState(newRepresentation, transition, args);
                _onTransitionCompletedEvent.Invoke(transition);

            }
            else
            {
                _onTransitionedEvent.Invoke(transition);
                representation = EnterState(newRepresentation, transition, args);
                _onTransitionCompletedEvent.Invoke(transition);
            }
            transition.Context.State = representation.UnderlyingState;
        }

        private void HandleTransitioningTrigger( object[] args, StateRepresentation representativeState, Transition transition)
        {
            transition = representativeState.Exit(transition);

            transition.Context.State = transition.Destination;
            var newRepresentation = GetRepresentation(transition.Destination);

            //Alert all listeners of state transition
            _onTransitionedEvent.Invoke(transition);
            var representation = EnterState(newRepresentation, transition, args);

            // Check if state has changed by entering new state (by firing triggers in OnEntry or such)
            if (!representation.UnderlyingState.Equals(transition.Context.State))
            {
                // The state has been changed after entering the state, must update current state to new one
                transition.Context.State = representation.UnderlyingState;
            }

            _onTransitionCompletedEvent.Invoke(CreateTransition(transition.Source, transition.Context.State, transition.Trigger, transition.Context, transition.Parameters));
        }

        private StateRepresentation EnterState(StateRepresentation representation, Transition transition, object [] args)
        {
            // Enter the new state
            representation.Enter(transition, args);

            if (FiringMode.Immediate.Equals(_firingMode) && !transition.Context.State.Equals(transition.Destination))
            {
                // This can happen if triggers are fired in OnEntry
                // Must update current representation with updated State
                representation = GetRepresentation(transition.Context.State);
                transition = CreateTransition(transition.Source, transition.Context.State, transition.Trigger, transition.Context, args);
            }

            // Recursively enter substates that have an initial transition
            if (representation.HasInitialTransition)
            {
                // Verify that the target state is a substate
                // Check if state has substate(s), and if an initial transition(s) has been set up.
                if (!representation.GetSubstates().Any(s => s.UnderlyingState.Equals(representation.InitialTransitionTarget)))
                {
                    throw new InvalidOperationException($"The target ({representation.InitialTransitionTarget}) for the initial transition is not a substate.");
                }

                var initialTransition = new InitialTransition(transition.Source, representation.InitialTransitionTarget, transition.Trigger, transition.Context, args);
                representation = GetRepresentation(representation.InitialTransitionTarget);

                // Alert all listeners of initial state transition
                _onTransitionedEvent.Invoke(CreateTransition(transition.Destination, initialTransition.Destination, transition.Trigger, transition.Context, transition.Parameters));
                representation = EnterState(representation, initialTransition, args);
            }

            return representation;
        }

        /// <summary>
        /// Override the default behaviour of throwing an exception when an unhandled trigger
        /// is fired.
        /// </summary>
        /// <param name="unhandledTriggerAction">An action to call when an unhandled trigger is fired.</param>
        public void OnUnhandledTrigger(Action<TState, TTrigger> unhandledTriggerAction)
        {
            if (unhandledTriggerAction == null) throw new ArgumentNullException(nameof(unhandledTriggerAction));
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync((s, t, c) => unhandledTriggerAction(s, t));
        }

        /// <summary>
        /// Override the default behaviour of throwing an exception when an unhandled trigger
        /// is fired.
        /// </summary>
        /// <param name="unhandledTriggerAction">An action to call when an unhandled trigger is fired.</param>
        public void OnUnhandledTrigger(Action<TState, TTrigger, ICollection<string>> unhandledTriggerAction)
        {
            if (unhandledTriggerAction == null) throw new ArgumentNullException(nameof(unhandledTriggerAction));
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync(unhandledTriggerAction);
        }

        /// <summary>
        /// Determine if the state machine is in the supplied state.
        /// </summary>
        /// <param name="state">The state to test for.</param>
        /// <returns>True if the current state is equal to, or a substate of,
        /// the supplied state.</returns>
        public bool IsInState(TState currentState, TState state)
        {
            return GetRepresentation(currentState).IsIncludedIn(state);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state.
        /// </summary>
        /// <param name="trigger">Trigger to test.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TState currentState, TTrigger trigger)
        {
            return GetRepresentation(currentState).CanHandle(trigger);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state.
        /// </summary>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TState currentState, TTrigger trigger, out ICollection<string> unmetGuards)
        {
            return GetRepresentation(currentState).CanHandle(trigger, new object[] { }, out unmetGuards);
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0> SetTriggerParameters<TArg0>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0, TArg1> SetTriggerParameters<TArg0, TArg1>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0, TArg1>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0, TArg1, TArg2> SetTriggerParameters<TArg0, TArg1, TArg2>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0, TArg1, TArg2>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        void SaveTriggerConfiguration(TriggerWithParameters trigger)
        {
            if (_triggerConfiguration.ContainsKey(trigger.Trigger))
                throw new InvalidOperationException(
                    string.Format(StateMachineResources.CannotReconfigureParameters, trigger));

            _triggerConfiguration.Add(trigger.Trigger, trigger);
        }

        void DefaultUnhandledTriggerAction(TState state, TTrigger trigger, ICollection<string> unmetGuardConditions)
        {
            if (unmetGuardConditions?.Any() ?? false)
                throw new InvalidOperationException(
                    string.Format(
                        StateMachineResources.NoTransitionsUnmetGuardConditions,
                        trigger, state, string.Join(", ", unmetGuardConditions)));

            throw new InvalidOperationException(
                string.Format(
                    StateMachineResources.NoTransitionsPermitted,
                    trigger, state));
        }

        /// <summary>
        /// Registers a callback that will be invoked every time the state machine
        /// transitions from one state into another.
        /// </summary>
        /// <param name="onTransitionAction">The action to execute, accepting the details
        /// of the transition.</param>
        public void OnTransitioned(Action<Transition> onTransitionAction)
        {
            if (onTransitionAction == null) throw new ArgumentNullException(nameof(onTransitionAction));
            _onTransitionedEvent.Register(onTransitionAction);
        }

        /// <summary>
        /// Registers a callback that will be invoked every time the statemachine
        /// transitions from one state into another and all the OnEntryFrom etc methods
        /// have been invoked
        /// </summary>
        /// <param name="onTransitionAction">The action to execute, accepting the details
        /// of the transition.</param>
        public void OnTransitionCompleted(Action<Transition> onTransitionAction)
        {
            if (onTransitionAction == null) throw new ArgumentNullException(nameof(onTransitionAction));
            _onTransitionCompletedEvent.Register(onTransitionAction);
        }
    }
}
