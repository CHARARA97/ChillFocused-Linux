using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The Unity-side <see cref="IJsonCodec"/>. Kept separate so the client and the
    /// poller can be compiled into the Unity-free test project.
    /// </summary>
    public sealed class JsonUtilityCodec : IJsonCodec
    {
        public T Decode<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }

        public string Encode<T>(T value)
        {
            return JsonUtility.ToJson(value);
        }
    }
}
