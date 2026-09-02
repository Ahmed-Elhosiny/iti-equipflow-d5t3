namespace EquipFlow.Domain;

public class Equipment
{
    // الـ Id يتم توليده تلقائياً ولا يمكن تعديله من الخارج
    public Guid Id { get; } 
    
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }

    // Constructor يضمن أن كل Equipment له Id فريد فور إنشائه
    public Equipment()
    {
        Id = Guid.NewGuid();
    }
}