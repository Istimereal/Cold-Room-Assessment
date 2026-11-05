import { request } from "./http";

export type FacilityListItem = {
  id: string;
  name: string;
};

export type EquipmentSummary = {
  id: string;
  equipmentTypeId: string;
  name: string;
  placement: string;
};

export type FacilityDetail = {
  id: string;
  name: string;
  equipment: EquipmentSummary[];
};

const fallback = "The analysis service could not be reached.";

export function listFacilities(signal?: AbortSignal): Promise<FacilityListItem[]> {
  return request<FacilityListItem[]>("/api/facilities", { signal }, fallback);
}

export function getFacility(facilityId: string, signal?: AbortSignal): Promise<FacilityDetail> {
  return request<FacilityDetail>(
    `/api/facilities/${encodeURIComponent(facilityId)}`,
    { signal },
    "The equipment list could not be loaded.",
  );
}
