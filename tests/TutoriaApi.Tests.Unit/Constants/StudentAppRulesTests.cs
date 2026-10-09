using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using Xunit;

namespace TutoriaApi.Tests.Unit.Constants;

public class StudentAppRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Theory]
    [InlineData(2008, 10, 7, 18)]  // birthday today
    [InlineData(2008, 10, 8, 17)]  // birthday tomorrow
    [InlineData(2010, 1, 1, 16)]
    public void AgeOn_ComputesCompletedYears(int y, int m, int d, int expected)
        => Assert.Equal(expected, StudentApp.AgeOn(new DateTime(y, m, d), Today));

    [Fact]
    public void IsMinor_AdultBirthDateButSignalUnder18_IsMinor()
    {
        var profile = new StudentProfile { AgeSignalUpper = 15 };
        Assert.True(StudentApp.IsMinor(new DateTime(2000, 1, 1), profile, Today));
        Assert.False(StudentApp.IsMinor(new DateTime(2000, 1, 1), new StudentProfile(), Today));
    }

    [Fact]
    public void IsMinor_MinorBirthDateAndAdultSignal_StaysMinor()
        => Assert.True(StudentApp.IsMinor(new DateTime(2012, 1, 1), new StudentProfile { AgeSignalLower = 18 }, Today));

    [Fact]
    public void BelowMinAge_OnlyFromSignalUpperBound()
    {
        Assert.True(StudentApp.BelowMinAge(new StudentProfile { AgeSignalUpper = 12 }));
        Assert.False(StudentApp.BelowMinAge(new StudentProfile { AgeSignalUpper = 15 }));
        Assert.False(StudentApp.BelowMinAge(null));
    }

    [Fact]
    public void GuardianOk_MinorNeedsApproval()
    {
        var minor = new DateTime(2010, 1, 1);
        Assert.False(StudentApp.GuardianOk(minor, new StudentProfile { GuardianStatus = "pending" }, Today));
        Assert.True(StudentApp.GuardianOk(minor, new StudentProfile { GuardianStatus = "approved" }, Today));
    }

    [Fact]
    public void ActivePlan_ExpiredOrMissing_IsNull()
    {
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.Null(StudentApp.ActivePlan(null, now));
        Assert.Null(StudentApp.ActivePlan(new StudentSubscription { Plan = "pro", PeriodEnd = now.AddSeconds(-1) }, now));
        Assert.Equal("pro", StudentApp.ActivePlan(new StudentSubscription { Plan = "pro", PeriodEnd = now.AddDays(1) }, now)!.Id);
    }

    [Fact]
    public void AllowedAreas_SimplesUsesChoice_ProGetsAll_UniversityNone()
    {
        var simples = StudentApp.FindPlan("simples");
        var profile = new StudentProfile { SelectedAreas = "matematica,linguagens,bogus" };
        Assert.Equal(new[] { "linguagens", "matematica" }, StudentApp.AllowedAreas(simples, profile));
        Assert.Equal(StudentApp.AreaIds, StudentApp.AllowedAreas(StudentApp.FindPlan("pro"), null));
        Assert.Empty(StudentApp.AllowedAreas(StudentApp.FindPlan("universitario"), null));
    }

    [Fact]
    public void NeedsAreaChoice_SimplesWithoutTwoAreas()
    {
        var simples = StudentApp.FindPlan("simples");
        Assert.True(StudentApp.NeedsAreaChoice(simples, new StudentProfile { SelectedAreas = "humanas" }));
        Assert.False(StudentApp.NeedsAreaChoice(simples, new StudentProfile { SelectedAreas = "humanas,natureza" }));
        Assert.False(StudentApp.NeedsAreaChoice(StudentApp.FindPlan("pro"), new StudentProfile()));
    }

    [Fact]
    public void CanChangeAreas_OncePerPeriod()
    {
        var period = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = new StudentSubscription { PeriodStart = period };
        Assert.True(StudentApp.CanChangeAreas(new StudentProfile(), sub)); // first choice
        Assert.True(StudentApp.CanChangeAreas(new StudentProfile { SelectedAreas = "humanas,natureza" }, sub));
        Assert.False(StudentApp.CanChangeAreas(new StudentProfile { SelectedAreas = "humanas,natureza", AreasChangedPeriodStart = period }, sub));
    }

    [Theory]
    [InlineData("2026-10-07T18:00:00Z", "2026-10-04T03:00:00Z")] // Wednesday 15:00 BRT → Sunday 00:00 BRT
    [InlineData("2026-10-11T03:00:00Z", "2026-10-11T03:00:00Z")] // Sunday 00:00 BRT starts a new week
    [InlineData("2026-10-11T02:59:00Z", "2026-10-04T03:00:00Z")] // Saturday 23:59 BRT is still last week
    public void WeekStartUtc_SundayMidnightBrasilia(string now, string expected)
        => Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), StudentApp.WeekStartUtc(DateTime.Parse(now).ToUniversalTime()));
}
