using ColdRoomAssessment.Application.Events;
using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.IntegrationTests.Events;

public sealed class RoomEventLogParserTests
{
    [Fact]
    public void Door_and_product_actions_parse_and_zero_duration_is_accepted()
    {
        var parsed = RoomEventLogParser.Parse(
            """
            Date;Action;Start Time;End Time
            2024-06-01;Door opened;10:00:01;10:00:02
            2024-06-01;Door closed;10:00:15;10:00:15
            2024-06-01;Products in;10:01:00;10:02:00
            2024-06-01;Products out;10:03:00;10:03:30
            2024-06-01;Begin;09:00:00;09:00:00
            2024-06-01;End;18:00:00;18:00:00
            """);

        Assert.True(parsed.Succeeded);
        Assert.Equal(
            [
                RoomEventType.DoorOpened,
                RoomEventType.DoorClosed,
                RoomEventType.ProductsIn,
                RoomEventType.ProductsOut,
                RoomEventType.Begin,
                RoomEventType.End
            ],
            parsed.Events.Select(item => item.Type));
        var closed = parsed.Events[1];
        Assert.Equal(closed.From, closed.To);
        Assert.Equal(DateTimeKind.Unspecified, closed.From.Kind);
        Assert.Equal(new DateTime(2024, 6, 1, 10, 0, 15), closed.From);
        Assert.Equal(new DateTime(2024, 6, 1, 10, 1, 0), parsed.Events[2].From);
        Assert.Equal(new DateTime(2024, 6, 1, 10, 2, 0), parsed.Events[2].To);
    }

    [Theory]
    [InlineData("2024/06/01;Door opened;10:00:00;10:00:01")]
    [InlineData("01.06.2024;Door opened;10:00:00;10:00:01")]
    [InlineData("2024-06-01;Door opened;10:00;10:00:01")]
    [InlineData("2024-06-01;Door opened;10:00:00;10:00")]
    public void Bad_date_or_time_is_rejected(string row)
    {
        var parsed = RoomEventLogParser.Parse("Date;Action;Start Time;End Time\n" + row);

        Assert.False(parsed.Succeeded);
        Assert.Empty(parsed.Events);
        Assert.Contains("A date or time is invalid.", parsed.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Window opened")]
    [InlineData("door opened")]
    [InlineData("Products In")]
    [InlineData("Door open")]
    public void Unknown_action_is_rejected(string action)
    {
        var parsed = RoomEventLogParser.Parse(
            $"Date;Action;Start Time;End Time\n2024-06-01;{action};10:00:00;10:00:01");

        Assert.False(parsed.Succeeded);
        Assert.Empty(parsed.Events);
        Assert.Contains("The action was not recognized.", parsed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_later_bad_row_rejects_the_whole_file()
    {
        var parsed = RoomEventLogParser.Parse(
            """
            Date;Action;Start Time;End Time
            2024-06-01;Door opened;10:00:00;10:00:01
            2024-06-01;Window opened;10:00:02;10:00:03
            """);

        Assert.False(parsed.Succeeded);
        Assert.Empty(parsed.Events);
    }

    [Fact]
    public void From_after_to_is_rejected()
    {
        var parsed = RoomEventLogParser.Parse(
            "Date;Action;Start Time;End Time\n2024-06-01;Door opened;10:00:05;10:00:01");

        Assert.False(parsed.Succeeded);
        Assert.Contains("From is after to.", parsed.Error, StringComparison.Ordinal);
    }
}
