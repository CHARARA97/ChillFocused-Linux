namespace ChillFocused.Core
{
    /// <summary>
    /// The JSON boundary between the plugin and its side effects.
    /// </summary>
    /// <remarks>
    /// Responses are flat DTOs shaped for Unity's JsonUtility, but the client
    /// itself should not know that. Injecting the codec keeps the HTTP/queue logic
    /// testable without UnityEngine, and leaves the Unity adapter in one small file.
    /// </remarks>
    public interface IJsonCodec
    {
        T Decode<T>(string json);
        string Encode<T>(T value);
    }
}
