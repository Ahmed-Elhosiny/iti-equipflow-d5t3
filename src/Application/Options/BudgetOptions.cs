namespace EquipFlow.Application.Options;

public class BudgetOptions
{
    public const string SectionName = "Budget";
    
    /// <summary>
    /// The day of the month (1-31) when user budgets should reset.
    /// If the month has fewer days, it will clamp to the last day of the month.
    /// </summary>
    public int ResetDayOfMonth { get; set; } = 1;
}