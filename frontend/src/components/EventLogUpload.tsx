import { useEffect, useState } from "react";
import { visibleError } from "../api/http";
import { uploadRoomEvents } from "../api/roomEvents";

type EventLogUploadProps = {
  facilityId: string;
};

const fallback = "The event log could not be uploaded.";

export function EventLogUpload({ facilityId }: EventLogUploadProps) {
  const [file, setFile] = useState<File | null>(null);
  const [fileKey, setFileKey] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [importedCount, setImportedCount] = useState<number | null>(null);

  useEffect(() => {
    setFile(null);
    setFileKey((key) => key + 1);
    setError(null);
    setImportedCount(null);
    setLoading(false);
  }, [facilityId]);

  async function submit() {
    if (facilityId.length === 0 || file === null) {
      return;
    }

    setLoading(true);
    setError(null);
    setImportedCount(null);
    try {
      const result = await uploadRoomEvents(facilityId, file);
      setImportedCount(result.importedCount);
      setFile(null);
      setFileKey((key) => key + 1);
    } catch (caught: unknown) {
      if (caught instanceof DOMException && caught.name === "AbortError") {
        return;
      }

      setError(visibleError(caught, fallback));
    } finally {
      setLoading(false);
    }
  }

  return (
    <form
      className="controls"
      aria-label="Upload room event log"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <label>
        Event log
        <input
          key={fileKey}
          type="file"
          accept=".csv,text/csv"
          onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setError(null);
            setImportedCount(null);
          }}
          disabled={loading}
        />
      </label>
      <button type="submit" disabled={loading || facilityId.length === 0 || file === null}>
        {loading ? "Uploading event log…" : "Upload event log"}
      </button>
      <p className="quiet">
        Room events stay separate from sensor measurements. Upload does not run analysis or AI.
      </p>
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      {importedCount !== null ? (
        <p className="feedback success" role="status">
          Imported {importedCount} room {importedCount === 1 ? "event" : "events"}.
        </p>
      ) : null}
    </form>
  );
}
