namespace ChillFocused.Core
{
    /// <summary>The narrow surface RuntimeHost needs from the Unity runner.</summary>
    /// <remarks>
    /// The host must recreate a destroyed Unity component without depending on
    /// MonoBehaviour itself. Keeping this interface small lets the lifecycle tests
    /// use a fake runner, while the real FocusRunner remains the only Unity object.
    /// </remarks>
    internal interface IFocusRunner
    {
        bool IsAlive { get; }
        FocusSettings CurrentSettings { get; }
        void SetClient(IFocusedEndpoint client);
        void ApplySettings(FocusSettings settings);
        void Tick();
        void ShowToast(string message, bool warning);
        void StopExternalSession();
        PluginDebugSnapshot CaptureDebug();
        void DestroyRunner();
    }
}
