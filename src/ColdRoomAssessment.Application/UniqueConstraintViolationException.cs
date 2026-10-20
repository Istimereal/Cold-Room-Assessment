namespace ColdRoomAssessment.Application;

public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(Exception innerException)
        : base("A unique constraint was violated.", innerException)
    {
    }
}
