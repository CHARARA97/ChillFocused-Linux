using ChillFocused.Core;
using Xunit;

namespace NestopiSystem.DIContainers
{
    // Mirrors the game's actual shape: Resolve<T> is an instance method and the
    // scope is reached through the static Inst property.
    public sealed class ProjectLifetimeScope
    {
        public static ProjectLifetimeScope Inst = new ProjectLifetimeScope();

        public T Resolve<T>() where T : new()
        {
            return new T();
        }
    }
}

namespace ChillFocused.Tests
{
    internal sealed class FakeService
    {
    }

    public class GameServicesInstanceTests
    {
        [Fact]
        public void Resolves_through_the_game_scope_instance()
        {
            GameServices.Reset();
            var service = GameServices.Resolve("ChillFocused.Tests.FakeService", true);
            Assert.NotNull(service);
            Assert.IsType<FakeService>(service);
        }
    }
}
