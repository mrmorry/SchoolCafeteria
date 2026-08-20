using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Typed accessor over SystemSetting. Currency, tax handling, low-balance defaults and feature
/// toggles all live here — never hardcoded in application code.
/// </summary>
public class SettingsService
{
    private readonly IAppDbContext _db;

    public SettingsService(IAppDbContext db) => _db = db;

    public async Task<string> GetStringAsync(Guid schoolId, string key, string defaultValue, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.Key == key, ct);
        return setting?.Value ?? defaultValue;
    }

    public async Task<bool> GetBoolAsync(Guid schoolId, string key, bool defaultValue, CancellationToken ct = default)
    {
        var value = await GetStringAsync(schoolId, key, defaultValue.ToString(), ct);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(Guid schoolId, string key, decimal defaultValue, CancellationToken ct = default)
    {
        var value = await GetStringAsync(schoolId, key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        return decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
    }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(Guid schoolId, CancellationToken ct = default)
        => await _db.SystemSettings.Where(s => s.SchoolId == schoolId).ToListAsync(ct);

    public async Task SetAsync(Guid schoolId, string key, string value, string valueType, string? description, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.Key == key, ct);
        if (setting is null)
        {
            _db.SystemSettings.Add(new SystemSetting { SchoolId = schoolId, Key = key, Value = value, ValueType = valueType, Description = description });
        }
        else
        {
            setting.Value = value;
            setting.ValueType = valueType;
            if (description is not null) setting.Description = description;
        }
        await _db.SaveChangesAsync(ct);
    }
}
