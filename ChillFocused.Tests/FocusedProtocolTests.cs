using System.Text.Json;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The plugin reads Focused's <c>/api/v1/focus</c> payload, which is flat by
    /// contract. A nested block would arrive null in the game -- that bug kept the
    /// gate checkbox frozen for a whole release -- so the shape is asserted here,
    /// and the Python side asserts that every field below exists in the payload it
    /// actually serves.
    /// </summary>
    public class FocusStateTests
    {
        private const string Sample =
            "{\"ok\":true,\"active\":true,\"dry_run\":false,\"pid\":4242," +
            "\"version\":\"0.1.0\",\"frozen_count\":2,\"frozen_total\":7," +
            "\"frozen_names\":[\"firefox\",\"vivaldi\"]," +
            "\"frozen_units\":[\"app-firefox-1.scope\",\"\"]," +
            "\"recent_names\":[\"firefox\"],\"protect_count\":443," +
            "\"delegated_source\":\"game-timer\"}";

        private static FocusStateResponse Parse(string json)
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<FocusStateResponse>(json, options);
        }

        [Fact]
        public void TheSessionAndTheSuspendedListArriveAsPrimitives()
        {
            var state = Parse(Sample);

            Assert.True(state.active);
            Assert.False(state.dry_run);
            Assert.Equal(4242, state.pid);
            Assert.Equal("0.1.0", state.version);
            Assert.Equal(2, state.frozen_count);
            Assert.Equal(7, state.frozen_total);
            Assert.Equal("firefox", state.frozen_names[0]);
            Assert.Equal("app-firefox-1.scope", state.frozen_units[0]);
            Assert.Equal("firefox", state.recent_names[0]);
            Assert.Equal(443, state.protect_count);
            Assert.Equal("game-timer", state.delegated_source);
        }

        [Fact]
        public void AnOlderBackendWithoutTheFieldsReadsAsIdle()
        {
            var state = Parse("{\"ok\":true}");

            Assert.False(state.active);
            Assert.Equal(0, state.frozen_count);
            Assert.Null(state.frozen_names);
        }

        [Fact]
        public void ThePluginOnlyNeedsFlatFields()
        {
            // Anything nested would silently arrive null under Unity's JsonUtility,
            // so every field of this DTO has to be a scalar or a primitive array.
            var allowed = new[]
            {
                typeof(bool), typeof(int), typeof(float), typeof(string),
                typeof(string[]), typeof(int[]), typeof(bool[]),
            };

            foreach (var field in typeof(FocusStateResponse).GetFields())
            {
                Assert.Contains(field.FieldType, allowed);
            }
        }
    }

    /// <summary>
    /// The process picker depends on this mapping. It exists because an
    /// object-array that Unity's JsonUtility would not bind left the list empty
    /// with no error anywhere.
    /// </summary>
    public class ProcessListResponseTests
    {
        private const string Sample =
            "{\"ok\":true," +
            "\"names\":[\"firefox\",\"wineserver\",\"cava\"]," +
            "\"counts\":[3,1,1]," +
            "\"protected_flags\":[false,true,false]," +
            "\"protect_rules\":[\"\",\"protect.name:wineserver\",\"\"]," +
            "\"processes\":[{\"name\":\"firefox\",\"count\":3,\"protected\":false,\"protect_rule\":\"\"}]}";

        private static ProcessListResponse Parse(string json)
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<ProcessListResponse>(json, options);
        }

        [Fact]
        public void ParallelArraysBecomeItems()
        {
            var items = Parse(Sample).ToItems();

            Assert.Equal(3, items.Length);
            Assert.Equal("firefox", items[0].name);
            Assert.Equal(3, items[0].count);
            Assert.False(items[0].@protected);
        }

        [Fact]
        public void ProtectedFlagIsCarriedAcross()
        {
            var items = Parse(Sample).ToItems();

            Assert.True(items[1].@protected);
            Assert.Equal("protect.name:wineserver", items[1].protect_rule);
        }

        [Fact]
        public void TheObjectsArrayIsIgnored()
        {
            // The Focused still sends "processes" for other clients. Unity's JsonUtility
            // leaves that array null here, so the plugin reads the parallel arrays only
            // and an objects-only response yields nothing rather than a half-built list.
            var json = "{\"ok\":true,\"processes\":[{\"name\":\"chrome\",\"count\":5," +
                       "\"protected\":false,\"protect_rule\":\"\"}]}";

            Assert.Empty(Parse(json).ToItems());
        }

        [Fact]
        public void RaggedArraysDoNotThrow()
        {
            // counts shorter than names must not become an index out of range.
            var json = "{\"ok\":true,\"names\":[\"a\",\"b\",\"c\"],\"counts\":[7]}";

            var items = Parse(json).ToItems();

            Assert.Equal(3, items.Length);
            Assert.Equal(7, items[0].count);
            Assert.Equal(1, items[1].count);
        }

        [Fact]
        public void EmptyResponseYieldsNoItems()
        {
            Assert.Empty(Parse("{\"ok\":true,\"names\":[],\"processes\":[]}").ToItems());
            Assert.Empty(new ProcessListResponse().ToItems());
        }
    }
}
