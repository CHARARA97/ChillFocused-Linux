using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// The small slice of Focused the in-game UI may call.
    /// </summary>
    /// <remarks>
    /// The settings window and the process picker used to take <see cref="FocusedClient"/>
    /// directly, which dragged HTTP, Unity's JsonUtility and the queueing model into
    /// files that only draw widgets. This port keeps the drawing layer replaceable
    /// and makes the UI's side effects explicit.
    /// </remarks>
    public interface IOverlayActions
    {
        bool Busy { get; }
        string BaseUrl { get; }
        void GetProcessesAsync(int limit, Action<ProcessListResponse, string> done);
        void ThawAllAsync(Action<SimpleResponse, string> done);
        void ForceScanAsync(Action<ScanResponse, string> done);
    }
}
