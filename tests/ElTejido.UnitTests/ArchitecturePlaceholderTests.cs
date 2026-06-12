using FluentAssertions;

namespace ElTejido.UnitTests;

public sealed class ArchitecturePlaceholderTests
{
    [Fact]
    public void TestProject_IsReadyForDomainSpecifications()
    {
        const string requirement = "REQ 31.8";

        requirement.Should().StartWith("REQ");
    }
}
