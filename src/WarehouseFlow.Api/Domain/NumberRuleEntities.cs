namespace WarehouseFlow.Api.Domain;

public sealed class DocumentNumberRule
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string DateFormat { get; set; } = "yyyyMMdd";
    public string Separator { get; set; } = "-";
    public int SequenceLength { get; set; } = 4;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentNumberSequence
{
    public long Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string DateKey { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
}
