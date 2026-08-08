namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;

public sealed record ProtectRequest(string Value);
public sealed record ProtectResponse(string TenantId, string Protected, string Unprotected);
