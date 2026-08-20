using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.Infrastructure.Adapters;

/// <summary>
/// No concrete school SIS/ERP was specified for v1 (see docs/01-analisis.md, open question #4).
/// This adapter fulfils the IStudentSourceAdapter contract for a manually uploaded CSV file so the
/// scheduled-sync / API-push code paths are exercisable; a real SIS integration becomes an
/// additional adapter (e.g. ApiStudentSourceAdapter) registered via DI without touching
/// Application or Domain.
/// </summary>
public class CsvStudentSourceAdapter : IStudentSourceAdapter
{
    public string SourceName => "csv-upload";

    public Task<IReadOnlyList<ExternalStudentRecord>> FetchAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExternalStudentRecord>>(Array.Empty<ExternalStudentRecord>());
}
