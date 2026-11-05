import { request } from "./http";

export type ImportStatus = "Pending" | "Completed" | "Failed";

export type ImportSummary = {
  id: string;
  equipmentId: string;
  equipmentName: string;
  originalFileName: string | null;
  importedAt: string | null;
  firstMeasurementAt: string | null;
  lastMeasurementAt: string | null;
  validRowCount: number;
  invalidRowCount: number;
  measurementCount: number;
  status: ImportStatus;
};

const fallback = "The file could not be uploaded.";

export function uploadMeasurements(
  equipmentId: string,
  file: File,
  signal?: AbortSignal,
): Promise<ImportSummary> {
  const body = new FormData();
  body.append("file", file, file.name);
  return request<ImportSummary>(
    `/api/equipment/${encodeURIComponent(equipmentId)}/imports`,
    { method: "POST", body, signal },
    fallback,
  );
}
