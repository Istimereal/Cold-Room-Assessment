namespace ColdRoomAssessment.Application.Verification;

public sealed class VerifierNotConfiguredException : Exception
{
    public VerifierNotConfiguredException()
        : base("Verifier is not configured.")
    {
    }
}

public sealed class VerifierUnavailableException : Exception
{
    public VerifierUnavailableException()
        : base("The verifier did not return a result.")
    {
    }
}

public enum VerifierInvalidReason
{
    Output,
    Evidence,
    Reference
}

public sealed class VerifierInvalidResultException : Exception
{
    public VerifierInvalidResultException(VerifierInvalidReason reason)
        : base(MessageFor(reason))
    {
        Reason = reason;
    }

    public VerifierInvalidReason Reason { get; }

    public static string MessageFor(VerifierInvalidReason reason) =>
        reason switch
        {
            VerifierInvalidReason.Evidence => "The verifier cited evidence that is not in the catalog.",
            VerifierInvalidReason.Reference =>
                "The verifier cited an observation or hypothesis that is not in the investigation.",
            _ => "The verifier returned an invalid result."
        };
}
