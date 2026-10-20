using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public sealed class FacilityService(IColdRoomStore store)
{
    public async Task<OperationResult<FacilityResponse>> CreateAsync(string? name, CancellationToken cancellationToken)
    {
        if (!TextField.TryTrim(name, "Name", TextField.NameMaxLength, out var trimmed, out var error))
        {
            return OperationResult<FacilityResponse>.Invalid(error!);
        }

        var facility = new Facility
        {
            Id = Guid.CreateVersion7(),
            Name = trimmed
        };

        await store.AddFacilityAsync(facility, cancellationToken);
        return OperationResult<FacilityResponse>.Success(FacilityResponse.From(facility));
    }

    public async Task<OperationResult<FacilityResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var facility = await store.GetFacilityAsync(id, cancellationToken);
        if (facility is null)
        {
            return OperationResult<FacilityResponse>.NotFound("Facility was not found.");
        }

        return OperationResult<FacilityResponse>.Success(FacilityResponse.From(facility));
    }
}
