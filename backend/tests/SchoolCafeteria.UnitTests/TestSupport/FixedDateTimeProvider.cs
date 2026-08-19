using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.UnitTests.TestSupport;

public class FixedDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
}
