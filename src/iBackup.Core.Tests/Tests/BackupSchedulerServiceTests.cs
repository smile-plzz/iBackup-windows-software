namespace iBackup.Core.Tests.Tests;

using System;
using FluentAssertions;
using iBackup.Core.Interfaces;
using iBackup.Core.Models;
using iBackup.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class BackupSchedulerServiceTests
{
    private readonly Mock<IManifestRepository> _mockRepo;
    private readonly BackupSchedulerService _service;

    public BackupSchedulerServiceTests()
    {
        _mockRepo = new Mock<IManifestRepository>();
        _service = new BackupSchedulerService(_mockRepo.Object, NullLogger<BackupSchedulerService>.Instance);
    }

    [Fact]
    public void ShouldTrigger_ReturnsFalse_WhenScheduleIsDisabled()
    {
        _service.CurrentSchedule.IsEnabled = false;
        var now = new DateTime(2026, 11, 3, 21, 0, 0);

        bool result = _service.ShouldTrigger(now, isOnAcPower: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrigger_ReturnsFalse_WhenOnBatteryAndRequireAcPowerIsTrue()
    {
        _service.CurrentSchedule.IsEnabled = true;
        _service.CurrentSchedule.OnlyOnAcPower = true;
        _service.CurrentSchedule.Frequency = ScheduleFrequency.Daily;
        _service.CurrentSchedule.ScheduledTime = new TimeSpan(21, 0, 0);

        var now = new DateTime(2026, 11, 3, 21, 0, 0);
        bool result = _service.ShouldTrigger(now, isOnAcPower: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrigger_ReturnsTrue_WhenDailyScheduleMatchesHourAndMinute()
    {
        _service.CurrentSchedule.IsEnabled = true;
        _service.CurrentSchedule.OnlyOnAcPower = false;
        _service.CurrentSchedule.Frequency = ScheduleFrequency.Daily;
        _service.CurrentSchedule.ScheduledTime = new TimeSpan(14, 30, 0);

        var now = new DateTime(2026, 11, 3, 14, 30, 45);
        bool result = _service.ShouldTrigger(now, isOnAcPower: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldTrigger_ReturnsFalse_WhenAlreadyRunInSameMinute()
    {
        var now = new DateTime(2026, 11, 3, 21, 0, 15);
        _service.CurrentSchedule.IsEnabled = true;
        _service.CurrentSchedule.Frequency = ScheduleFrequency.Daily;
        _service.CurrentSchedule.ScheduledTime = new TimeSpan(21, 0, 0);
        _service.CurrentSchedule.LastRunTime = new DateTime(2026, 11, 3, 21, 0, 5);

        bool result = _service.ShouldTrigger(now, isOnAcPower: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrigger_ReturnsTrue_ForWeeklyScheduleOnMatchingDay()
    {
        // 2026-11-08 is a Sunday
        var sunday = new DateTime(2026, 11, 8, 20, 0, 0);
        _service.CurrentSchedule.IsEnabled = true;
        _service.CurrentSchedule.OnlyOnAcPower = false;
        _service.CurrentSchedule.Frequency = ScheduleFrequency.Weekly;
        _service.CurrentSchedule.ScheduledDay = DayOfWeek.Sunday;
        _service.CurrentSchedule.ScheduledTime = new TimeSpan(20, 0, 0);

        bool result = _service.ShouldTrigger(sunday, isOnAcPower: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldTrigger_ReturnsFalse_ForWeeklyScheduleOnDifferentDay()
    {
        // 2026-11-03 is Tuesday
        var tuesday = new DateTime(2026, 11, 3, 20, 0, 0);
        _service.CurrentSchedule.IsEnabled = true;
        _service.CurrentSchedule.Frequency = ScheduleFrequency.Weekly;
        _service.CurrentSchedule.ScheduledDay = DayOfWeek.Sunday;
        _service.CurrentSchedule.ScheduledTime = new TimeSpan(20, 0, 0);

        bool result = _service.ShouldTrigger(tuesday, isOnAcPower: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextRunTime_ReturnsNextDay_WhenScheduledTimeHasPassedToday()
    {
        var schedule = new BackupSchedule
        {
            IsEnabled = true,
            Frequency = ScheduleFrequency.Daily,
            ScheduledTime = new TimeSpan(10, 0, 0)
        };

        var now = new DateTime(2026, 11, 3, 14, 0, 0);
        var next = schedule.CalculateNextRunTime(now);

        next.Should().Be(new DateTime(2026, 11, 4, 10, 0, 0));
    }
}

