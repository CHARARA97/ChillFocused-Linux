using System;
namespace ChillFocused.Core
{
    /// <summary>
    /// Everything the overlay loop needs from the Focused endpoint.
    /// </summary>
    /// <remarks>
    /// FocusRunner used to hold the concrete FocusedClient just to call Pump and
    /// read BaseUrl. Combining the two existing read/UI ports here keeps the runner
    /// replaceable without creating another parallel API surface.
    /// </remarks>
    public interface IFocusedEndpoint : IStatusFeed, IOverlayActions
    {
        void Pump(int maxActions);
        void StopSessionAsync(Action<SimpleResponse, string> done);
    }
}
