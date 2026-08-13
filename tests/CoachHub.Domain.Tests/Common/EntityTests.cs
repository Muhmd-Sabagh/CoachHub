using CoachHub.Domain.Common;

namespace CoachHub.Domain.Tests.Common;

public sealed class EntityTests
{
    [Fact]
    public void New_entity_receives_non_empty_identifier()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    private sealed class TestEntity : Entity;
}
