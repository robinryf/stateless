using AlarmExample;
using Stateless;
using System.Diagnostics;

namespace AlarmExample
{
    /// <summary>
    /// A sample class that implements an alarm as a state machine using Stateless 
    /// (https://github.com/dotnet-state-machine/stateless).
    /// 
    /// It also shows one way that temporary states can be implemented with the use of 
    /// Timers. PreArmed, PreTriggered, Triggered, and ArmPaused are "temporary" states with
    /// a configurable delay (i.e. to allow for an "arm delay"... a delay between Disarmed
    /// and Armed). The Triggered state is also considered temporary, since if an alarm 
    /// sounds for a certain period of time and no-one Acknowledges it, the state machine
    /// returns to the Armed state.
    /// 
    /// Timers are triggered via OnEntry() and OnExit() methods. Transitions are written to
    /// the Trace in order to show what happens.
    /// 
    /// The included PNG file shows what the state flow looks like.
    /// 
    /// </summary>
    public partial class Alarm : StateMachine<AlarmState, AlarmCommand, Alarm>.IStateMachineContext
    {
        /// <summary>
        /// Moves the Alarm into the provided <see cref="AlarmState" /> via the defined <see cref="AlarmCommand" />.
        /// </summary>
        /// <param name="command">The <see cref="AlarmCommand" /> to execute on the current <see cref="AlarmState" />.</param>
        /// <returns>The new <see cref="AlarmState" />.</returns>
        public AlarmState ExecuteTransition(AlarmCommand command)
        {
            if (_handle.CanFire(command))
            {
                _handle.Fire(command);
            }
            else
            {
                throw new InvalidOperationException($"Cannot transition from via {command}");
            }

            return State;
        }
        
        public AlarmState State { get; set; }
        

        /// <summary>
        /// Defines whether the <see cref="Alarm"/> has been configured.
        /// </summary>
        public bool IsConfigured { get; private set; }

        /// <summary>
        /// Returns whether the provided command is a valid transition from the Current State.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public bool CanFireCommand(AlarmCommand command) 
        { 
            return _handle.CanFire(command); 
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="armDelay">The time (in seconds) the alarm will spend in the
        /// Prearmed status before continuing to the Armed status (if not transitioned to
        /// Disarmed via Disarm).</param>
        /// <param name="pauseDelay">The time (in seconds) the alarm will spend in the 
        /// ArmPaused status before returning to Armed (if not transitioned to Triggered 
        /// via Trigger).</param>
        /// <param name="triggerDelay">The time (in seconds) the alarm will spend in the
        /// PreTriggered status before continuing to the Triggered status (if not 
        /// transitioned to Disarmed via Disarm).</param>
        /// <param name="triggerTimeOut">The time (in seconds) the alarm will spend in the
        /// Triggered status before returning to the Armed status (if not transitioned to
        /// Disarmed via Disarm).</param>
        public Alarm(int armDelay, int pauseDelay, int triggerDelay, int triggerTimeOut)
        {

            preArmTimer = new System.Timers .Timer(armDelay * 1000) { AutoReset = false, Enabled = false };
            preArmTimer.Elapsed += TimeoutTimerElapsed;
            pauseTimer = new System.Timers.Timer(pauseDelay * 1000) { AutoReset = false, Enabled = false };
            pauseTimer.Elapsed += TimeoutTimerElapsed;
            triggerDelayTimer = new System.Timers.Timer(triggerDelay * 1000) { AutoReset = false, Enabled = false };
            triggerDelayTimer.Elapsed += TimeoutTimerElapsed;
            triggerTimeOutTimer = new System.Timers.Timer(triggerTimeOut * 1000) { AutoReset = false, Enabled = false };
            triggerTimeOutTimer.Elapsed += TimeoutTimerElapsed;

            CreateMachine();

            _handle = _machine!.CreateHandle(this, AlarmState.Undefined);
            _handle.Fire(AlarmCommand.Startup);
        }

        private static void CreateMachine()
        {
	        if (_machine != null)
	        {
		        return;
	        }
	        _machine = new StateMachine<AlarmState, AlarmCommand, Alarm>();
	        _machine.OnTransitioned(OnTransition);

	        _machine.Configure(AlarmState.Undefined)
		        .Permit(AlarmCommand.Startup, AlarmState.Disarmed)
		        .OnExit((transition) => transition.Context.IsConfigured = true);

	        _machine.Configure(AlarmState.Disarmed)
		        .Permit(AlarmCommand.Arm, AlarmState.Prearmed);

	        _machine.Configure(AlarmState.Armed)
		        .Permit(AlarmCommand.Disarm, AlarmState.Disarmed)
		        .Permit(AlarmCommand.Trigger, AlarmState.PreTriggered)
		        .Permit(AlarmCommand.Pause, AlarmState.ArmPaused);

	        _machine.Configure(AlarmState.Prearmed)
		        .OnEntry(transition => transition.Context.ConfigureTimer(true, transition.Context.preArmTimer, "Pre-arm"))
		        .OnExit((transition) => transition.Context.ConfigureTimer(false, transition.Context.preArmTimer, "Pre-arm"))
		        .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
		        .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);

	        _machine.Configure(AlarmState.ArmPaused)
		        .OnEntry((transition) => transition.Context.ConfigureTimer(true, transition.Context.pauseTimer, "Pause delay"))
		        .OnExit((transition) => transition.Context.ConfigureTimer(false, transition.Context.pauseTimer, "Pause delay"))
		        .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
		        .Permit(AlarmCommand.Trigger, AlarmState.PreTriggered);

	        _machine.Configure(AlarmState.Triggered)
		        .OnEntry((transition) => transition.Context.ConfigureTimer(true, transition.Context.triggerTimeOutTimer, "Trigger timeout"))
		        .OnExit((transition) => transition.Context.ConfigureTimer(false, transition.Context.triggerTimeOutTimer, "Trigger timeout"))
		        .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
		        .Permit(AlarmCommand.Acknowledge, AlarmState.Acknowledged);

	        _machine.Configure(AlarmState.PreTriggered)
		        .OnEntry((transition) => transition.Context.ConfigureTimer(true, transition.Context.triggerDelayTimer, "Trigger delay"))
		        .OnExit((transition) => transition.Context.ConfigureTimer(false, transition.Context.triggerDelayTimer, "Trigger delay"))
		        .Permit(AlarmCommand.TimeOut, AlarmState.Triggered)
		        .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);

	        _machine.Configure(AlarmState.Acknowledged)
		        .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);
        }

        private void TimeoutTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            _handle.Fire(AlarmCommand.TimeOut);
        }

        private void ConfigureTimer(bool active, System.Timers.Timer timer, string timerName)
        {
            if (timer != null)
                if (active)
                {
                    timer.Start();
                    Console.WriteLine($"{timerName} started.");
                }
                else
                {
                    timer.Stop();
                    Console.WriteLine($"{timerName} cancelled.");
                }
        }

        private static void OnTransition(StateMachine<AlarmState, AlarmCommand, Alarm>.Transition transition)
        {
            Console.WriteLine($"Transitioned {transition.Context} from {transition.Source} to " +
                $"{transition.Destination} via {transition.Trigger}.");
        }
        
        private static StateMachine<AlarmState, AlarmCommand, Alarm>? _machine;
        private System.Timers.Timer? preArmTimer;
        private System.Timers.Timer? pauseTimer;
        private System.Timers.Timer? triggerDelayTimer;
        private System.Timers.Timer? triggerTimeOutTimer;
        private StateMachine<AlarmState, AlarmCommand, Alarm>.StateMachineHandle _handle;
    }
}