import type { SpotResponse } from '../../../core/models/chart.model';
import { ChartCell } from './ChartCell';

interface ChartGridProps {
  spot: SpotResponse;
}

const RANKS = ['A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2'];

export function ChartGrid({ spot }: ChartGridProps) {
  const handMap = new Map(spot.hands.map((h) => [h.handLabel, h.action]));

  function getHandLabel(row: number, col: number): string {
    const r = RANKS[row];
    const c = RANKS[col];
    if (row === col) return `${r}${c}`;
    if (row < col) return `${r}${c}s`;
    return `${c}${r}o`;
  }

  return (
    <div className="chart-grid-wrapper">
      <div className="chart-grid">
        <div className="chart-header-row">
          <div className="chart-corner" />
          {RANKS.map((r) => (
            <div key={r} className="chart-header-cell">{r}</div>
          ))}
        </div>
        {RANKS.map((rowRank, rowIdx) => (
          <div key={rowRank} className="chart-row">
            <div className="chart-header-cell">{rowRank}</div>
            {RANKS.map((_colRank, colIdx) => (
              <ChartCell
                key={`${rowIdx}-${colIdx}`}
                handLabel={getHandLabel(rowIdx, colIdx)}
                action={handMap.get(getHandLabel(rowIdx, colIdx)) || 'UNKNOWN'}
              />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
