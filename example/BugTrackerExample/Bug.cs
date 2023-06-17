using System;
using Stateless;
using Stateless.Graph;

namespace BugTrackerExample
{
    public class Bug : StateMachine<Bug.BugState, Bug.Trigger, Bug>.IStateMachineContext
    {
        public enum BugState { Open, Assigned, Deferred, Closed }

        private enum Trigger { Assign, Defer, Close }

        private readonly StateMachine<BugState, Trigger, Bug> _machine;
        // The TriggerWithParameters object is used when a trigger requires a payload.
        private readonly StateMachine<BugState, Trigger, Bug>.TriggerWithParameters<string> _assignTrigger;

        private readonly string _title;
        private string _assignee;
        private StateMachine<BugState, Trigger, Bug>.StateMachineHandle _handle;

        public BugState State { get; set; }


        /// <summary>
        /// Constructor for the Bug class
        /// </summary>
        /// <param name="title">The title of the bug report</param>
        public Bug(string title)
        {
            _title = title;

            // Instantiate a new state machine in the Open state
            _machine = new StateMachine<BugState, Trigger, Bug>();

            // Instantiate a new trigger with a parameter. 
            _assignTrigger = _machine.SetTriggerParameters<string>(Trigger.Assign);

            // Configure the Open state
            _machine.Configure(BugState.Open)
                .Permit(Trigger.Assign, BugState.Assigned);

            // Configure the Assigned state
            _machine.Configure(BugState.Assigned)
                .SubstateOf(BugState.Open)
                .OnEntryFrom(_assignTrigger, OnAssigned)  // This is where the TriggerWithParameters is used. Note that the TriggerWithParameters object is used, not something from the enum
                .PermitReentry(Trigger.Assign)
                .Permit(Trigger.Close, BugState.Closed)
                .Permit(Trigger.Defer, BugState.Deferred)
                .OnExit(OnDeassigned);

            // Configure the Deferred state
            _machine.Configure(BugState.Deferred)
                .OnEntry(() => _assignee = null)
                .Permit(Trigger.Assign, BugState.Assigned);

            _handle = _machine.CreateHandle(this, BugState.Open);
        }

        public void Close()
        {
            _handle.Fire(Trigger.Close);
        }

        public void Assign(string assignee)
        {
            // This is how a trigger with parameter is used, the parameter is supplied to the state machine as a parameter to the Fire method.
            _handle.Fire(_assignTrigger, assignee);
        }

        public bool CanAssign => _handle.CanFire(Trigger.Assign);

        public void Defer()
        {
	        _handle.Fire(Trigger.Defer);
        }
        /// <summary>
        /// This method is called automatically when the Assigned state is entered, but only when the trigger is _assignTrigger.
        /// </summary>
        /// <param name="assignee"></param>
        private void OnAssigned(string assignee)
        {
            if (_assignee != null && assignee != _assignee)
                SendEmailToAssignee("Don't forget to help the new employee!");

            _assignee = assignee;
            SendEmailToAssignee("You own it.");
        }
        /// <summary>
        /// This method is called when the state machine exits the Assigned state
        /// </summary>
        private void OnDeassigned()
        {
            SendEmailToAssignee("You're off the hook.");
        }

        private void SendEmailToAssignee(string message)
        {
            Console.WriteLine("{0}, RE {1}: {2}", _assignee, _title, message);
        }

        public string ToDotGraph()
        {
            return UmlDotGraph.Format(_handle.GetInfo());
        }
    }
}
