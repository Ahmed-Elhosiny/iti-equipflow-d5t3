using EquipFlow.Domain;
using Xunit;

namespace EquipFlow.Domain.Tests;

public class EquipmentTests
{
    [Fact]
    public void Equipment_Should_Have_Name()
    {
        // Arrange
        var equipment = new Equipment { Name = "Pump-101" };

        // Act & Assert
        Assert.Equal("Pump-101", equipment.Name);
        Assert.NotEqual(Guid.Empty, equipment.Id);
    }
}