namespace Stateless.Reflection
{
    /// <summary>
    /// Information on entry and exit actions
    /// </summary>
    public class ActionInfo
    {
        /// <summary>
        /// The method invoked during the action (entry or exit)
        /// </summary>
        public InvocationInfo Method { get; internal set; }

        /// <summary>
        /// If non-null, specifies the "from" trigger that must be present for this method to be invoked
        /// </summary>
        public string FromTrigger { get; internal set; }

        internal static ActionInfo Create<TState, TTrigger, TContext>(StateMachine<TState, TTrigger, TContext>.EntryActionBehavior entryAction) where TContext : StateMachine<TState, TTrigger, TContext>.IStateMachineContext
        {
            StateMachine<TState, TTrigger, TContext>.EntryActionBehavior.SyncFrom<TTrigger> syncFrom = entryAction as StateMachine<TState, TTrigger, TContext>.EntryActionBehavior.SyncFrom<TTrigger>;
            if (syncFrom != null)
                return new ActionInfo(entryAction.Description, syncFrom.Trigger.ToString());

            StateMachine<TState, TTrigger, TContext>.EntryActionBehavior.AsyncFrom<TTrigger> asyncFrom = entryAction as StateMachine<TState, TTrigger, TContext>.EntryActionBehavior.AsyncFrom<TTrigger>;
            if (asyncFrom != null)
                return new ActionInfo(entryAction.Description, asyncFrom.Trigger.ToString());

            return new ActionInfo(entryAction.Description, null);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ActionInfo(InvocationInfo method, string fromTrigger)
        {
            Method = method;
            FromTrigger = fromTrigger;
        }
    }
}
