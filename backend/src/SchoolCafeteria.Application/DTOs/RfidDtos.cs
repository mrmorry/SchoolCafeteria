namespace SchoolCafeteria.Application.DTOs;

public record IssueRfidRequest(Guid BuyerId, string RawUid);
public record ReplaceRfidRequest(Guid BuyerId, string NewRawUid, string Reason);
public record BlockRfidRequest(Guid RfidCredentialId, string Reason);
public record UnblockRfidRequest(Guid RfidCredentialId, string Reason);
public record ReportLostRfidRequest(Guid RfidCredentialId, string Reason);

public record RfidLookupResult(Guid BuyerId, string BuyerName, string BuyerType, Guid WalletId,
    decimal WalletBalance, string WalletStatus, string RfidMaskedValue, bool AllowedToPurchase, string? BlockReason);

public record RfidCredentialDto(Guid Id, Guid BuyerId, string BuyerName, string MaskedValue, string Status,
    DateTime IssuedAtUtc, DateTime? BlockedAtUtc, string? BlockReason);

public record ManualBuyerSearchRequest(string Query, string Reason);
