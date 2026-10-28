namespace EquipFlow.Application.Options;

public sealed class CostGovernorOptions
{
    public const string SectionName = "CostGovernor";

    /// <summary>
    /// The default monthly token budget assigned to new/seeded users.
    /// </summary>
    public int DefaultMonthlyTokenBudget { get; set; } = 100000;

    public bool HardCutOff { get; set; } = true;
    
    public decimal SafetyMargin { get; set; } = 1.2m;
}