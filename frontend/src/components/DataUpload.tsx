import type { EquipmentSummary, FacilityListItem } from "../api/facilities";
import type { ImportSummary } from "../api/imports";
import { formatTimestamp } from "../format";
import { StatusBadge } from "./StatusBadge";

type DataUploadProps = {
  facilities: FacilityListItem[];
  facilitiesLoading: boolean;
  facilitiesError: string | null;
  facilityId: string;
  equipment: EquipmentSummary[];
  equipmentLoading: boolean;
  equipmentError: string | null;
  equipmentId: string;
  fileInputKey: number;
  hasFile: boolean;
  uploadLoading: boolean;
  uploadError: string | null;
  uploadResult: ImportSummary | null;
  onFacilityId: (facilityId: string) => void;
  onEquipmentId: (equipmentId: string) => void;
  onFile: (file: File | null) => void;
  onUpload: () => void;
};

export function DataUpload({
  facilities,
  facilitiesLoading,
  facilitiesError,
  facilityId,
  equipment,
  equipmentLoading,
  equipmentError,
  equipmentId,
  fileInputKey,
  hasFile,
  uploadLoading,
  uploadError,
  uploadResult,
  onFacilityId,
  onEquipmentId,
  onFile,
  onUpload,
}: DataUploadProps) {
  const equipmentReady = facilityId.length > 0 && !equipmentLoading;
  return (
    <div className="upload">
      <form
        className="controls"
        onSubmit={(event) => {
          event.preventDefault();
          onUpload();
        }}
      >
        <label>
          Facility
          <select
            value={facilityId}
            onChange={(event) => onFacilityId(event.target.value)}
            disabled={facilitiesLoading || facilities.length === 0 || uploadLoading}
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
          Equipment
          <select
            value={equipmentId}
            onChange={(event) => onEquipmentId(event.target.value)}
            disabled={!equipmentReady || equipment.length === 0 || uploadLoading}
          >
            <option value="">{equipmentLoading ? "Loading equipment…" : "Select equipment"}</option>
            {equipment.map((item) => (
              <option key={item.id} value={item.id}>
                {item.placement.length > 0 ? `${item.name} — ${item.placement}` : item.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          CSV file
          <input
            key={fileInputKey}
            type="file"
            accept=".csv,text/csv"
            onChange={(event) => onFile(event.target.files?.[0] ?? null)}
            disabled={uploadLoading}
          />
        </label>
        <button type="submit" disabled={uploadLoading || equipmentId.length === 0 || !hasFile}>
          {uploadLoading ? "Uploading…" : "Upload data"}
        </button>
      </form>
      {facilitiesError ? (
        <p className="error" role="alert">
          {facilitiesError}
        </p>
      ) : null}
      {equipmentError ? (
        <p className="error" role="alert">
          {equipmentError}
        </p>
      ) : null}
      {uploadError ? (
        <p className="error" role="alert">
          {uploadError}
        </p>
      ) : null}
      {!facilitiesLoading && !facilitiesError && facilities.length === 0 ? (
        <p className="quiet">No facilities are available.</p>
      ) : null}
      {equipmentReady && !equipmentError && equipment.length === 0 ? (
        <p className="quiet">This facility has no equipment.</p>
      ) : null}
      <p className="quiet">Upload stores the file for the selected equipment. It does not run analysis or AI.</p>
      {uploadLoading ? <p role="status">Uploading…</p> : null}
      {uploadResult ? <UploadSummary summary={uploadResult} /> : null}
    </div>
  );
}

function UploadSummary({ summary }: { summary: ImportSummary }) {
  return (
    <div className="card upload-result" role="status">
      <h3>Import complete</h3>
      <dl className="upload-summary">
        {summary.originalFileName ? (
          <div>
            <dt>File</dt>
            <dd>{summary.originalFileName}</dd>
          </div>
        ) : null}
        <div>
          <dt>Imported source rows</dt>
          <dd>{summary.validRowCount}</dd>
        </div>
        <div>
          <dt>Measurements</dt>
          <dd>{summary.measurementCount}</dd>
        </div>
        <div>
          <dt>Invalid rows</dt>
          <dd>{summary.invalidRowCount}</dd>
        </div>
        {summary.importedAt ? (
          <div>
            <dt>Imported at</dt>
            <dd>{formatTimestamp(summary.importedAt)}</dd>
          </div>
        ) : null}
        <div>
          <dt>Status</dt>
          <dd>
            <StatusBadge tone={statusTone(summary.status)}>{summary.status}</StatusBadge>
          </dd>
        </div>
      </dl>
    </div>
  );
}

function statusTone(status: ImportSummary["status"]) {
  if (status === "Completed") {
    return "complete" as const;
  }

  if (status === "Failed") {
    return "incomplete" as const;
  }

  return "neutral" as const;
}
