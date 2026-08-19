using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class ImportJob : SchoolScopedEntity
{
    public string FileName { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Student";
    public ImportMode Mode { get; set; } = ImportMode.CreateOrUpdate;
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Uploaded;

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public int DuplicateRows { get; set; }
    public int ImportedRows { get; set; }

    public string ExecutedByUserId { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? ResultFileUrl { get; set; }

    public ICollection<ImportJobRow> Rows { get; set; } = new List<ImportJobRow>();
}

public class ImportJobRow : BaseEntity
{
    public Guid ImportJobId { get; set; }
    public ImportJob? ImportJob { get; set; }

    public int RowNumber { get; set; }
    public string RawDataJson { get; set; } = string.Empty;
    public ImportRowStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NaturalKey { get; set; } // e.g. StudentCode, used for idempotent re-imports
}

/// <summary>Represents an external SIS/ERP that can push buyers via API. Auth material is stored hashed/rotable.</summary>
public class ExternalSystem : SchoolScopedEntity
{
    public string Name { get; set; } = string.Empty;

    [Sensitive]
    public string ApiKeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAtUtc { get; set; }
}

public class IntegrationLog : SchoolScopedEntity
{
    public Guid ExternalSystemId { get; set; }
    public string Direction { get; set; } = "Inbound"; // Inbound | Outbound
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
