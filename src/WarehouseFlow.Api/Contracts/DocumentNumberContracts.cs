namespace WarehouseFlow.Api.Contracts;

public sealed record UpdateDocumentNumberRuleRequest(string Name, string Prefix, string DateFormat, string Separator, int SequenceLength, bool IsActive);
