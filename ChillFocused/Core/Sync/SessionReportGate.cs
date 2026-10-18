using System.Threading;

namespace ChillFocused.Core
{
    internal sealed class SessionReportGate : ISessionReportGate
    {
        private int _external;
        private int _withdrawPending;

        public bool CanReport
        {
            get { return Volatile.Read(ref _external) == 0; }
        }

        public void EnterExternal()
        {
            Interlocked.Exchange(ref _external, 1);
            Interlocked.Exchange(ref _withdrawPending, 1);
        }

        public void LeaveExternal()
        {
            Interlocked.Exchange(ref _external, 0);
        }

        public bool TryConsumeWithdrawal()
        {
            return Interlocked.Exchange(ref _withdrawPending, 0) != 0;
        }
    }
}
