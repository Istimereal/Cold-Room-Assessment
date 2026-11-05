import type { FacilityListItem } from "../api/facilities";

type AnalysisControlsProps = {
  facilities: FacilityListItem[];
  facilitiesLoading: boolean;
  facilitiesError: string | null;
  facilityId: string;
  from: string;
  to: string;
  analysisLoading: boolean;
  onFacilityId: (facilityId: string) => void;
  onFrom: (from: string) => void;
  onTo: (to: string) => void;
  onAnalyze: () => void;
};

export function AnalysisControls({
  facilities,
  facilitiesLoading,
  facilitiesError,
  facilityId,
  from,
  to,
  analysisLoading,
  onFacilityId,
  onFrom,
  onTo,
  onAnalyze,
}: AnalysisControlsProps) {
  return (
    <form
      className="controls"
      onSubmit={(event) => {
        event.preventDefault();
        onAnalyze();
      }}
    >
      <label>
        Facility
        <select
          value={facilityId}
          onChange={(event) => onFacilityId(event.target.value)}
          disabled={facilitiesLoading || facilities.length === 0}
        >
          <option value="">{facilitiesLoading ? "Loading facilities…" : "Select a facility"}</option>
          {facilities.map((facility) => (
            <option key={facility.id} value={facility.id}>
              {facility.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        From
        <input
          type="datetime-local"
          step={1}
          value={from}
          onChange={(event) => onFrom(event.target.value)}
        />
      </label>
      <label>
        To
        <input type="datetime-local" step={1} value={to} onChange={(event) => onTo(event.target.value)} />
      </label>
      <button type="submit" disabled={analysisLoading || facilityId.length === 0}>
        {analysisLoading ? "Analyzing…" : "Analyze"}
      </button>
      {facilitiesError ? (
        <p className="error" role="alert">
          {facilitiesError}
        </p>
      ) : null}
      {!facilitiesLoading && !facilitiesError && facilities.length === 0 ? (
        <p className="quiet">No facilities are available.</p>
      ) : null}
    </form>
  );
}
