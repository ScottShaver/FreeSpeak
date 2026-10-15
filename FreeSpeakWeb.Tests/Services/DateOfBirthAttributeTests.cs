using FreeSpeakWeb.Validation;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="DateOfBirthAttribute"/>.
    /// </summary>
    public class DateOfBirthAttributeTests
    {
        private static readonly DateOnly Today = new(2026, 10, 15);

        [Fact]
        public void NullIsValid() => Assert.True(DateOfBirthAttribute.IsValidDateOfBirth(null, Today));

        [Fact]
        public void PastDateIsValid() => Assert.True(DateOfBirthAttribute.IsValidDateOfBirth(new DateOnly(1990, 1, 1), Today));

        [Fact]
        public void TodayIsValid() => Assert.True(DateOfBirthAttribute.IsValidDateOfBirth(Today, Today));

        [Fact]
        public void FutureDateIsInvalid() => Assert.False(DateOfBirthAttribute.IsValidDateOfBirth(new DateOnly(2999, 1, 1), Today));

        [Fact]
        public void TooOldDateIsInvalid() => Assert.False(DateOfBirthAttribute.IsValidDateOfBirth(new DateOnly(1899, 12, 31), Today));

        [Fact]
        public void IsValidRejectsFutureObject()
        {
            var attribute = new DateOfBirthAttribute();
            Assert.False(attribute.IsValid(new DateOnly(2999, 1, 1)));
            Assert.True(attribute.IsValid(new DateOnly(1990, 1, 1)));
            Assert.True(attribute.IsValid(null));
        }
    }
}
