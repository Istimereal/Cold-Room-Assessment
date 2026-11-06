import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../src/api/http";
import type { ImportSummary } from "../src/api/imports";

vi.mock("../src/api/facilities", () => ({
  listFacilities: vi.fn(),
  getFacility: vi.fn(),
  createFacility: vi.fn(),
}));

vi.mock("../src/api/equipmentTypes", () => ({
  listEquipmentTypes: vi.fn(),
  createEquipmentType: vi.fn(),
}));

vi.mock("../src/api/equipment", () => ({
  createEquipment: vi.fn(),
}));

vi.mock("../src/api/imports", () => ({
  uploadMeasurements: vi.fn(),
}));

vi.mock("../src/api/analysis", () => ({
  getAnalysis: vi.fn(),
}));

vi.mock("../src/api/investigations", () => ({
  runInvestigation: vi.fn(),
}));

vi.mock("../src/api/verifications", () => ({
  runVerification: vi.fn(),
}));

import { getAnalysis } from "../src/api/analysis";
import { createEquipment } from "../src/api/equipment";
import { createEquipmentType, listEquipmentTypes } from "../src/api/equipmentTypes";
import { createFacility, getFacility, listFacilities } from "../src/api/facilities";
import { uploadMeasurements } from "../src/api/imports";
import { runInvestigation } from "../src/api/investigations";
import { runVerification } from "../src/api/verifications";
import { App } from "../src/App";

const facility = { id: "fac-1", name: "Cold Room Experiment" };
const equipmentType = { id: "type-1", name: "DHT22 Logger" };
const equipment = {
  id: "equip-1",
  facilityId: facility.id,
  facilityName: facility.name,
  equipmentTypeId: equipmentType.id,
  equipmentTypeName: equipmentType.name,
  name: "SENSOR01",
  placement: "Cold room position 1",
};
const equipmentSummary = {
  id: equipment.id,
  equipmentTypeId: equipment.equipmentTypeId,
  name: equipment.name,
  placement: equipment.placement,
};

function selectValue(label: string): string {
  return (screen.getByLabelText(label) as HTMLSelectElement).value;
}

async function renderApp() {
  const user = userEvent.setup();
  render(<App />);
  await screen.findByRole("option", { name: "Select a facility" });
  await screen.findByRole("option", { name: "Select an equipment type" });
  return user;
}

describe("setup flow", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(listFacilities).mockResolvedValue([]);
    vi.mocked(listEquipmentTypes).mockResolvedValue([]);
    vi.mocked(getFacility).mockResolvedValue({ id: facility.id, name: facility.name, equipment: [] });
  });

  it("creates a facility, type, and equipment, then uploads to the selected equipment", async () => {
    let equipmentCreated = false;
    vi.mocked(createFacility).mockResolvedValue({ ...facility, equipment: [] });
    vi.mocked(listFacilities).mockResolvedValueOnce([]).mockResolvedValue([facility]);
    vi.mocked(createEquipmentType).mockResolvedValue(equipmentType);
    vi.mocked(listEquipmentTypes).mockResolvedValueOnce([]).mockResolvedValue([equipmentType]);
    vi.mocked(createEquipment).mockImplementation(async () => {
      equipmentCreated = true;
      return equipment;
    });
    vi.mocked(getFacility).mockImplementation(async (id: string) => ({
      id,
      name: facility.name,
      equipment: equipmentCreated && id === facility.id ? [equipmentSummary] : [],
    }));
    const uploadSummary: ImportSummary = {
      id: "import-1",
      equipmentId: equipment.id,
      equipmentName: equipment.name,
      originalFileName: "OTHER.CSV",
      importedAt: "2024-01-01T00:00:00",
      firstMeasurementAt: null,
      lastMeasurementAt: null,
      validRowCount: 2,
      invalidRowCount: 0,
      measurementCount: 4,
      status: "Completed",
    };
    vi.mocked(uploadMeasurements).mockResolvedValue(uploadSummary);

    const user = await renderApp();
    expect((screen.getByRole("button", { name: "Create equipment" }) as HTMLButtonElement).disabled).toBe(true);

    await user.type(screen.getByLabelText("Facility name"), "Cold Room Experiment");
    await user.click(screen.getByRole("button", { name: "Create facility" }));

    await screen.findByText("Created Cold Room Experiment. It is selected.");
    expect(selectValue("Facility")).toBe(facility.id);
    expect(vi.mocked(listFacilities).mock.calls.length).toBeGreaterThan(1);
    expect(screen.queryByRole("option", { name: "SENSOR01 — Cold room position 1" })).toBeNull();

    await user.type(screen.getByLabelText("Equipment type name"), "DHT22 Logger");
    await user.click(screen.getByRole("button", { name: "Create equipment type" }));
    expect(await screen.findByRole("option", { name: "DHT22 Logger" })).toBeTruthy();
    expect(selectValue("Equipment type")).toBe(equipmentType.id);

    await user.type(screen.getByLabelText("Equipment name"), "SENSOR01");
    await user.type(screen.getByLabelText("Placement"), "Cold room position 1");
    await user.click(screen.getByRole("button", { name: "Create equipment" }));

    expect(await screen.findByRole("option", { name: "SENSOR01 — Cold room position 1" })).toBeTruthy();
    expect(selectValue("Equipment")).toBe(equipment.id);
    expect(vi.mocked(createEquipment)).toHaveBeenCalledWith(facility.id, {
      equipmentTypeId: equipmentType.id,
      name: "SENSOR01",
      placement: "Cold room position 1",
    });

    const file = new File(["timestamp,temperature"], "OTHER.CSV", { type: "text/csv" });
    await user.upload(screen.getByLabelText("CSV file"), file);
    await user.click(screen.getByRole("button", { name: "Upload data" }));

    expect(vi.mocked(uploadMeasurements)).toHaveBeenCalledTimes(1);
    expect(vi.mocked(uploadMeasurements)).toHaveBeenCalledWith(equipment.id, file);
    expect(await screen.findByText("Import complete")).toBeTruthy();
    expect(screen.getByText("OTHER.CSV")).toBeTruthy();
    expect(vi.mocked(getAnalysis)).not.toHaveBeenCalled();
    expect(vi.mocked(runInvestigation)).not.toHaveBeenCalled();
    expect(vi.mocked(runVerification)).not.toHaveBeenCalled();
  });

  it("updates the equipment selector when the facility changes", async () => {
    vi.mocked(listFacilities).mockResolvedValue([facility, { id: "fac-2", name: "Other Room" }]);
    vi.mocked(getFacility).mockImplementation(async (id: string) => {
      if (id === facility.id) {
        return {
          id,
          name: facility.name,
          equipment: [
            {
              id: "equip-1",
              equipmentTypeId: equipmentType.id,
              name: "SENSOR01",
              placement: "Position 1",
            },
          ],
        };
      }

      return {
        id,
        name: "Other Room",
        equipment: [
          {
            id: "equip-2",
            equipmentTypeId: equipmentType.id,
            name: "SENSOR02",
            placement: "Position 2",
          },
        ],
      };
    });

    const user = await renderApp();
    await user.selectOptions(screen.getByLabelText("Facility"), facility.id);
    expect(await screen.findByRole("option", { name: "SENSOR01 — Position 1" })).toBeTruthy();

    await user.selectOptions(screen.getByLabelText("Facility"), "fac-2");
    expect(selectValue("Equipment")).toBe("");
    expect(await screen.findByRole("option", { name: "SENSOR02 — Position 2" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "SENSOR01 — Position 1" })).toBeNull();
  });

  it("shows controlled messages for invalid names, duplicates, a missing type, and an unavailable service", async () => {
    const user = await renderApp();
    vi.mocked(createFacility).mockRejectedValueOnce(new ApiError("Name is required."));
    await user.click(screen.getByRole("button", { name: "Create facility" }));
    const facilityForm = screen.getByRole("form", { name: "Create facility" });
    expect((await within(facilityForm).findByRole("alert")).textContent).toBe("Name is required.");

    vi.mocked(createEquipmentType).mockRejectedValueOnce(
      new ApiError("An equipment type named 'DHT22 Logger' already exists."),
    );
    await user.type(screen.getByLabelText("Equipment type name"), "DHT22 Logger");
    await user.click(screen.getByRole("button", { name: "Create equipment type" }));
    const typeForm = screen.getByRole("form", { name: "Create equipment type" });
    expect((await within(typeForm).findByRole("alert")).textContent).toBe(
      "An equipment type named 'DHT22 Logger' already exists.",
    );

    vi.mocked(createFacility).mockResolvedValue({ ...facility, equipment: [] });
    vi.mocked(listFacilities).mockResolvedValue([facility]);
    await user.type(screen.getByLabelText("Facility name"), "Cold Room Experiment");
    await user.click(screen.getByRole("button", { name: "Create facility" }));
    await screen.findByText("Created Cold Room Experiment. It is selected.");

    vi.mocked(createEquipment).mockRejectedValueOnce(new ApiError("Equipment type id is required."));
    await user.type(screen.getByLabelText("Equipment name"), "SENSOR01");
    await user.type(screen.getByLabelText("Placement"), "Cold room position 1");
    await user.click(screen.getByRole("button", { name: "Create equipment" }));
    const equipmentForm = screen.getByRole("form", { name: "Create equipment" });
    expect((await within(equipmentForm).findByRole("alert")).textContent).toBe("Equipment type id is required.");
    expect(vi.mocked(createEquipment)).toHaveBeenCalledWith(facility.id, {
      equipmentTypeId: undefined,
      name: "SENSOR01",
      placement: "Cold room position 1",
    });

    vi.mocked(createEquipmentType).mockResolvedValue(equipmentType);
    vi.mocked(listEquipmentTypes).mockResolvedValue([equipmentType]);
    await user.type(screen.getByLabelText("Equipment type name"), "DHT22 Logger");
    await user.click(screen.getByRole("button", { name: "Create equipment type" }));
    expect(await screen.findByRole("option", { name: "DHT22 Logger" })).toBeTruthy();

    vi.mocked(createEquipment).mockRejectedValueOnce(
      new ApiError("Equipment named 'SENSOR01' already exists in this facility."),
    );
    await user.click(screen.getByRole("button", { name: "Create equipment" }));
    expect((await within(equipmentForm).findByRole("alert")).textContent).toBe(
      "Equipment named 'SENSOR01' already exists in this facility.",
    );

    vi.mocked(createEquipment).mockRejectedValueOnce(new ApiError("Equipment type was not found."));
    await user.click(screen.getByRole("button", { name: "Create equipment" }));
    expect((await within(equipmentForm).findByRole("alert")).textContent).toBe("Equipment type was not found.");

    vi.mocked(createFacility).mockRejectedValueOnce(new Error("connect ECONNREFUSED\n    at internal stack"));
    await user.type(screen.getByLabelText("Facility name"), "Another Room");
    await user.click(screen.getByRole("button", { name: "Create facility" }));
    const unavailable = await within(facilityForm).findByRole("alert");
    expect(unavailable.textContent).toBe("The facility could not be created.");
    expect(unavailable.textContent).not.toContain("ECONNREFUSED");
    expect(unavailable.textContent).not.toContain("stack");
  });

  it("shows the backend message when the facility is missing", async () => {
    vi.mocked(listFacilities).mockResolvedValue([facility]);
    vi.mocked(createEquipment).mockRejectedValue(new ApiError("Facility was not found."));
    const user = await renderApp();
    await user.selectOptions(screen.getByLabelText("Facility"), facility.id);
    await user.type(screen.getByLabelText("Equipment name"), "SENSOR01");
    await user.type(screen.getByLabelText("Placement"), "Position 1");
    await user.click(screen.getByRole("button", { name: "Create equipment" }));
    const equipmentForm = screen.getByRole("form", { name: "Create equipment" });
    expect((await within(equipmentForm).findByRole("alert")).textContent).toBe("Facility was not found.");
    expect(vi.mocked(getAnalysis)).not.toHaveBeenCalled();
  });

  it("leaves room-event context off until it is chosen", async () => {
    await renderApp();
    const toggle = screen.getByRole("checkbox", { name: "Include known room events" }) as HTMLInputElement;
    expect(toggle.checked).toBe(false);
    const upload = screen.getByRole("button", { name: "Upload event log" }) as HTMLButtonElement;
    expect(upload.disabled).toBe(true);
  });
});
