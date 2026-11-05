type AnalysisControlsProps = {
  facilitySelected: boolean;
  from: string;
  to: string;
  analysisLoading: boolean;
  onFrom: (from: string) => void;
  onTo: (to: string) => void;
  onAnalyze: () => void;
};

export function AnalysisControls({
  facilitySelected,
  from,
  to,
  analysisLoading,
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
      <button type="submit" disabled={analysisLoading || !facilitySelected}>
        {analysisLoading ? "Analyzing…" : "Analyze"}
      </button>
    </form>
  );
}
