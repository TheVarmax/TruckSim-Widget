using System;

namespace ETSOverlay
{
    /// <summary>
    /// Measures accumulated active delivery duration, excluding paused or interrupted intervals.
    /// </summary>
    public class DeliveryActiveTimer
    {
        private TimeSpan _accumulated = TimeSpan.Zero;
        private bool _isRunning = false;
        private DateTime _lastTickUtc = DateTime.MinValue;
        private readonly object _lock = new();

        /// <summary>
        /// Gets the current accumulated active duration including time since last tick if currently running.
        /// </summary>
        public TimeSpan Elapsed => GetElapsed();

        public TimeSpan GetElapsed(DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            lock (_lock)
            {
                if (_isRunning && _lastTickUtc > DateTime.MinValue)
                {
                    TimeSpan delta = now - _lastTickUtc;
                    if (delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(15))
                    {
                        return _accumulated + delta;
                    }
                }
                return _accumulated;
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }

        /// <summary>
        /// Updates the active timer state. Called on every telemetry tick or status check.
        /// </summary>
        /// <param name="isActivelyDriving">True if cargo is attached, game online, unpaused, and in-game.</param>
        /// <param name="nowUtc">Optional explicit timestamp (useful for testing).</param>
        public void Update(bool isActivelyDriving, DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            lock (_lock)
            {
                if (isActivelyDriving)
                {
                    if (_isRunning)
                    {
                        if (_lastTickUtc > DateTime.MinValue)
                        {
                            TimeSpan delta = now - _lastTickUtc;
                            // Reject massive clock jumps (e.g. system sleep or clock modification)
                            if (delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(15))
                            {
                                _accumulated += delta;
                            }
                        }
                    }
                    _lastTickUtc = now;
                    _isRunning = true;
                }
                else
                {
                    if (_isRunning)
                    {
                        if (_lastTickUtc > DateTime.MinValue)
                        {
                            TimeSpan delta = now - _lastTickUtc;
                            if (delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(15))
                            {
                                _accumulated += delta;
                            }
                        }
                        _isRunning = false;
                        _lastTickUtc = DateTime.MinValue;
                    }
                }
            }
        }

        /// <summary>
        /// Flushes any pending elapsed duration into the accumulated total without stopping the timer.
        /// </summary>
        public void Flush(DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            lock (_lock)
            {
                if (_isRunning && _lastTickUtc > DateTime.MinValue)
                {
                    TimeSpan delta = now - _lastTickUtc;
                    if (delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(15))
                    {
                        _accumulated += delta;
                    }
                    _lastTickUtc = now;
                }
            }
        }

        /// <summary>
        /// Resets the timer to zero (e.g. when a new job starts or job is cancelled).
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _accumulated = TimeSpan.Zero;
                _isRunning = false;
                _lastTickUtc = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Restores the accumulated active duration from persisted job state.
        /// </summary>
        public void Restore(long accumulatedTicks)
        {
            lock (_lock)
            {
                _accumulated = accumulatedTicks > 0 ? TimeSpan.FromTicks(accumulatedTicks) : TimeSpan.Zero;
                _isRunning = false;
                _lastTickUtc = DateTime.MinValue;
            }
        }

        public long GetTicks() => Elapsed.Ticks;
    }
}
