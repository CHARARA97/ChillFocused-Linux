namespace ChillFocused.Core
{
    /// <summary>Thread-safe count/last/max timing for one pipeline stage.</summary>
    public sealed class PerfCounter
    {
        private readonly object _gate = new object();
        private int _count;
        private double _totalMs;
        private double _lastMs;
        private double _maxMs;

        public void Record(double milliseconds)
        {
            if (milliseconds < 0d)
            {
                milliseconds = 0d;
            }

            lock (_gate)
            {
                _count++;
                _lastMs = milliseconds;
                _totalMs += milliseconds;
                if (milliseconds > _maxMs)
                {
                    _maxMs = milliseconds;
                }
            }
        }

        public int Count
        {
            get { lock (_gate) { return _count; } }
        }

        public double LastMs
        {
            get { lock (_gate) { return _lastMs; } }
        }

        public double MaxMs
        {
            get { lock (_gate) { return _maxMs; } }
        }

        public double AverageMs
        {
            get { lock (_gate) { return _count == 0 ? 0d : _totalMs / _count; } }
        }
    }
}
