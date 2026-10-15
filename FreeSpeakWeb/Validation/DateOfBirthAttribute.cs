using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Validation
{
    /// <summary>
    /// Validates that a date of birth is not in the future and is not earlier than <see cref="MinimumYear"/>.
    /// Null values are considered valid (the field is optional).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DateOfBirthAttribute : ValidationAttribute
    {
        /// <summary>
        /// The earliest year accepted as a date of birth.
        /// </summary>
        public const int MinimumYear = 1900;

        /// <summary>
        /// Initializes a new instance with the default English error message.
        /// </summary>
        public DateOfBirthAttribute()
            : base("The {0} must be a date in the past, no earlier than " + MinimumYear + ".")
        {
        }

        /// <summary>
        /// Determines whether the supplied date of birth is acceptable.
        /// </summary>
        /// <param name="date">The date of birth, or null.</param>
        /// <param name="today">The current date; dates after it are rejected.</param>
        /// <returns>True when the date is null or between 1 January 1900 and <paramref name="today"/>.</returns>
        public static bool IsValidDateOfBirth(DateOnly? date, DateOnly today)
        {
            return date is null || (date.Value <= today && date.Value.Year >= MinimumYear);
        }

        /// <inheritdoc />
        public override bool IsValid(object? value)
        {
            var date = value switch
            {
                DateOnly d => d,
                DateTime dt => DateOnly.FromDateTime(dt),
                _ => (DateOnly?)null
            };

            // UTC+14 is the furthest ahead of UTC, so this never rejects a user's local "today".
            return IsValidDateOfBirth(date, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(14)));
        }
    }
}
