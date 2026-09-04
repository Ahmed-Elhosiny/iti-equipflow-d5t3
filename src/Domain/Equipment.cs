namespace EquipFlow.Domain;

public class Equipment
{
    public Guid Id { get; } 
    
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }

    public Equipment()
    {
        Id = Guid.NewGuid();
    }
}