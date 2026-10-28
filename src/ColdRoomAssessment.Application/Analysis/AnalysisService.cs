using ColdRoomAssessment.Application.Coverage;

namespace ColdRoomAssessment.Application.Analysis;

public sealed class AnalysisService(AnalysisDataService data)
{
    public async Task<OperationResult<FacilityAnalysis>> GetAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        CancellationToken cancellationToken)
    {
        var aligned = await data.GetAsync(facilityId, fromText, toText, cancellationToken);
        if (aligned.Status != OperationStatus.Success)
        {
            return new OperationResult<FacilityAnalysis>(aligned.Status, null, aligned.Error);
        }

        return OperationResult<FacilityAnalysis>.Success(MeasurementSeriesAnalyzer.Analyze(aligned.Value!));
    }
}
