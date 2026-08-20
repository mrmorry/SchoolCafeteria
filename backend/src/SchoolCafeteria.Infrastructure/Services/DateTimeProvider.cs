using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
