import { request } from "./http";

export type FacilityListItem = {
  id: string;
  name: string;
};

const fallback = "The analysis service could not be reached.";

export function listFacilities(signal?: AbortSignal): Promise<FacilityListItem[]> {
  return request<FacilityListItem[]>("/api/facilities", { signal }, fallback);
}
