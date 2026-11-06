using ColdRoomAssessment.Application.Events;

namespace ColdRoomAssessment.IntegrationTests.Events;

public sealed class RoomEventOverlapTests
{
    private static readonly DateTime From = new(2024, 6, 1, 10, 0, 0);
    private static readonly DateTime To = new(2024, 6, 1, 10, 5, 0);

    [Theory]
    [InlineData(10, 0, 0, 10, 0, 0, true)]
    [InlineData(10, 4, 59, 10, 4, 59, true)]
    [InlineData(9, 0, 0, 10, 0, 0, true)]
    [InlineData(10, 4, 0, 10, 6, 0, true)]
    [InlineData(10, 5, 0, 10, 5, 0, false)]
    [InlineData(9, 59, 59, 9, 59, 59, false)]
    [InlineData(10, 5, 0, 10, 10, 0, false)]
    public void Overlap_uses_a_half_open_window(
        int fromHour,
        int fromMinute,
        int fromSecond,
        int toHour,
        int toMinute,
        int toSecond,
        bool overlaps)
    {
        var eventFrom = new DateTime(2024, 6, 1, fromHour, fromMinute, fromSecond);
        var eventTo = new DateTime(2024, 6, 1, toHour, toMinute, toSecond);

        Assert.Equal(overlaps, RoomEventOverlap.Overlaps(eventFrom, eventTo, From, To));
    }
}
