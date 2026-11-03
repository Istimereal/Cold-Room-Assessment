namespace ColdRoomAssessment.Application.Investigation;

public sealed class InvestigatorNotConfiguredException : Exception
{
    public InvestigatorNotConfiguredException()
        : base("Investigator is not configured.")
    {
    }
}

public sealed class InvestigatorUnavailableException : Exception
{
    public InvestigatorUnavailableException()
        : base("The investigator did not return a result.")
    {
    }
}

public sealed class InvestigatorInvalidResultException : Exception
{
    public InvestigatorInvalidResultException()
        : base("The investigator returned an invalid result.")
    {
    }
}
