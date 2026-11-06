import { request } from "./http";

export type RoomEventImportSummary = {
  importedCount: number;
};

const fallback = "The event log could not be uploaded.";

export function uploadRoomEvents(
  facilityId: string,
  file: File,
  signal?: AbortSignal,
): Promise<RoomEventImportSummary> {
  const body = new FormData();
  body.append("file", file, file.name);
  return request<RoomEventImportSummary>(
    `/api/facilities/${encodeURIComponent(facilityId)}/events/import`,
    { method: "POST", body, signal },
    fallback,
  );
}
