import { useEffect, useState } from "react";
import type { Equipment } from "../api/equipment";
import type { EquipmentType } from "../api/equipmentTypes";
import type { FacilityDetail } from "../api/facilities";
import { visibleError } from "../api/http";

type CreateEquipmentInput = {
  equipmentTypeId: string;
  name: string;
  placement: string;
};

type SetupPanelProps = {
  facilityId: string;
  facilityName: string;
  equipmentTypes: EquipmentType[];
  equipmentTypesLoading: boolean;
  equipmentTypesError: string | null;
  onCreateFacility: (name: string) => Promise<FacilityDetail>;
  onCreateEquipmentType: (name: string) => Promise<EquipmentType>;
  onCreateEquipment: (input: CreateEquipmentInput) => Promise<Equipment>;
};

const facilityFallback = "The facility could not be created.";
const typeFallback = "The equipment type could not be created.";
const equipmentFallback = "The equipment could not be created.";

export function SetupPanel({
  facilityId,
  facilityName,
  equipmentTypes,
  equipmentTypesLoading,
  equipmentTypesError,
  onCreateFacility,
  onCreateEquipmentType,
  onCreateEquipment,
}: SetupPanelProps) {
  const [facilityNameInput, setFacilityNameInput] = useState("");
  const [facilityMessage, setFacilityMessage] = useState<string | null>(null);
  const [facilityError, setFacilityError] = useState<string | null>(null);
  const [facilitySaving, setFacilitySaving] = useState(false);
  const [typeName, setTypeName] = useState("");
  const [typeId, setTypeId] = useState("");
  const [typeMessage, setTypeMessage] = useState<string | null>(null);
  const [typeError, setTypeError] = useState<string | null>(null);
  const [typeSaving, setTypeSaving] = useState(false);
  const [equipmentName, setEquipmentName] = useState("");
  const [placement, setPlacement] = useState("");
  const [equipmentMessage, setEquipmentMessage] = useState<string | null>(null);
  const [equipmentError, setEquipmentError] = useState<string | null>(null);
  const [equipmentSaving, setEquipmentSaving] = useState(false);

  useEffect(() => {
    setEquipmentMessage(null);
    setEquipmentError(null);
  }, [facilityId]);

  async function submitFacility() {
    setFacilitySaving(true);
    setFacilityError(null);
    setFacilityMessage(null);
    try {
      const created = await onCreateFacility(facilityNameInput);
      setFacilityNameInput("");
      setFacilityMessage(`Created ${created.name}. It is selected.`);
    } catch (error: unknown) {
      if (isAbort(error)) {
        return;
      }

      setFacilityError(visibleError(error, facilityFallback));
    } finally {
      setFacilitySaving(false);
    }
  }

  async function submitType() {
    setTypeSaving(true);
    setTypeError(null);
    setTypeMessage(null);
    try {
      const created = await onCreateEquipmentType(typeName);
      setTypeName("");
      setTypeId(created.id);
      setTypeMessage(`Created ${created.name}. Choose it when you add equipment.`);
    } catch (error: unknown) {
      if (isAbort(error)) {
        return;
      }

      setTypeError(visibleError(error, typeFallback));
    } finally {
      setTypeSaving(false);
    }
  }

  async function submitEquipment() {
    if (facilityId.length === 0) {
      setEquipmentError("Select a facility first.");
      return;
    }

    setEquipmentSaving(true);
    setEquipmentError(null);
    setEquipmentMessage(null);
    try {
      const created = await onCreateEquipment({
        equipmentTypeId: typeId,
        name: equipmentName,
        placement,
      });
      setEquipmentName("");
      setPlacement("");
      setEquipmentMessage(
        `Created ${created.name}. It is selected under Upload data — choose ${created.name}.CSV and click Upload data.`,
      );
    } catch (error: unknown) {
      if (isAbort(error)) {
        return;
      }

      setEquipmentError(visibleError(error, equipmentFallback));
    } finally {
      setEquipmentSaving(false);
    }
  }

  const facilitySelected = facilityId.length > 0;
  const facilityLabel = facilityName.length > 0 ? facilityName : "the selected facility";

  return (
    <div className="setup">
      <p className="quiet setup-intro">
        Facility: where the experiment or data belongs. Equipment type: the logger or sensor model. Equipment: one
        concrete sensor or logger, such as SENSOR01.
      </p>
      <form
        className="controls"
        aria-label="Create facility"
        onSubmit={(event) => {
          event.preventDefault();
          void submitFacility();
        }}
      >
        <label>
          Facility name
          <input
            value={facilityNameInput}
            placeholder="Cold Room Experiment"
            autoComplete="off"
            onChange={(event) => {
              setFacilityNameInput(event.target.value);
              setFacilityMessage(null);
              setFacilityError(null);
            }}
            disabled={facilitySaving}
          />
        </label>
        <button type="submit" disabled={facilitySaving}>
          {facilitySaving ? "Creating facility…" : "Create facility"}
        </button>
        {facilityMessage ? (
          <p className="feedback success" role="status">
            {facilityMessage}
          </p>
        ) : null}
        {facilityError ? (
          <p className="feedback error" role="alert">
            {facilityError}
          </p>
        ) : null}
      </form>
      <form
        className="controls"
        aria-label="Create equipment type"
        onSubmit={(event) => {
          event.preventDefault();
          void submitType();
        }}
      >
        <label>
          Equipment type name
          <input
            value={typeName}
            placeholder="DHT22 Logger"
            autoComplete="off"
            onChange={(event) => {
              setTypeName(event.target.value);
              setTypeMessage(null);
              setTypeError(null);
            }}
            disabled={typeSaving}
          />
        </label>
        <button type="submit" disabled={typeSaving}>
          {typeSaving ? "Creating equipment type…" : "Create equipment type"}
        </button>
        {typeMessage ? (
          <p className="feedback success" role="status">
            {typeMessage}
          </p>
        ) : null}
        {typeError ? (
          <p className="feedback error" role="alert">
            {typeError}
          </p>
        ) : null}
        {equipmentTypesError ? (
          <p className="feedback error" role="alert">
            {equipmentTypesError}
          </p>
        ) : null}
      </form>
      <form
        className="controls"
        aria-label="Create equipment"
        onSubmit={(event) => {
          event.preventDefault();
          void submitEquipment();
        }}
      >
        <label>
          Equipment name
          <input
            value={equipmentName}
            placeholder="SENSOR01"
            autoComplete="off"
            onChange={(event) => {
              setEquipmentName(event.target.value);
              setEquipmentMessage(null);
              setEquipmentError(null);
            }}
            disabled={equipmentSaving}
          />
        </label>
        <label>
          Equipment type
          <select
            value={typeId}
            onChange={(event) => {
              setTypeId(event.target.value);
              setEquipmentMessage(null);
              setEquipmentError(null);
            }}
            disabled={equipmentTypesLoading || equipmentSaving}
          >
            <option value="">{equipmentTypesLoading ? "Loading equipment types…" : "Select an equipment type"}</option>
            {equipmentTypes.map((equipmentType) => (
              <option key={equipmentType.id} value={equipmentType.id}>
                {equipmentType.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Placement
          <input
            value={placement}
            placeholder="Cold room position 1"
            autoComplete="off"
            onChange={(event) => {
              setPlacement(event.target.value);
              setEquipmentMessage(null);
              setEquipmentError(null);
            }}
            disabled={equipmentSaving}
          />
        </label>
        <button type="submit" disabled={!facilitySelected || equipmentSaving}>
          {equipmentSaving ? "Creating equipment…" : "Create equipment"}
        </button>
        <p className="feedback quiet">
          {facilitySelected
            ? `Adding equipment to ${facilityLabel}.`
            : "Create a facility, or select one under Upload data."}
        </p>
        {equipmentMessage ? (
          <p className="feedback success" role="status">
            {equipmentMessage}
          </p>
        ) : null}
        {equipmentError ? (
          <p className="feedback error" role="alert">
            {equipmentError}
          </p>
        ) : null}
      </form>
    </div>
  );
}

function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}
