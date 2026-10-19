namespace WarehouseFlow.Api.Contracts;

public sealed record CreateCustomerRequest(string Code, string Name, string Contact = "", string Phone = "", string Address = "");
public sealed record UpdateCustomerRequest(string Name, string Contact, string Phone, string Address, bool IsActive);
public sealed record CreateSupplierRequest(string Code, string Name, string Contact = "", string Phone = "", string Address = "");
public sealed record UpdateSupplierRequest(string Name, string Contact, string Phone, string Address, bool IsActive);
