import { useState } from 'react';
import { useChart } from '../../../core/hooks/useChart';
import { AVAILABLE_STACKS } from '../../../core/constants/poker';
import { ChartControls } from './ChartControls';
import { ChartGrid } from './ChartGrid';
import { ChartResult } from './ChartResult';
import { Legend } from './Legend';

export function ChartsPage() {
  const [situation, setSituation] = useState('HU_SB_OR_FISH');
  const [stack, setStack] = useState('7bb');
  const [spot, setSpot] = useState<string | null>(null);
  const { chart, loading, error } = useChart(situation, stack);

  const spots = chart?.spots.map((s) => ({ value: s.spotKey, label: s.spotLabel })) || [];
  const selectedSpot = spot ? chart?.spots.find((s) => s.spotKey === spot) : chart?.spots[0];

  return (
    <div className="charts-page">
      <h1>Tablas Preflop</h1>
      <ChartControls
        situation={situation} stack={stack} stacks={AVAILABLE_STACKS}
        spots={spots} onSituationChange={setSituation}
        onStackChange={setStack} onSpotChange={setSpot} />
      {loading && <div className="loading">Cargando...</div>}
      {error && <div className="error">{error}</div>}
      {selectedSpot && (
        <>
          <ChartGrid spot={selectedSpot} />
          <ChartResult spot={selectedSpot} />
          <Legend />
        </>
      )}
    </div>
  );
}
