import { ACTION_COLORS } from '../../../core/constants/poker';
import type { SpotResponse } from '../../../core/models/chart.model';

interface ChartResultProps {
  spot: SpotResponse;
}

export function ChartResult({ spot }: ChartResultProps) {
  const isValid = spot.total === 169;

  return (
    <div className={`chart-result${isValid ? '' : ' chart-result--error'}`}>
      <h4>{spot.spotLabel}</h4>
      <div className="action-counts">
        {Object.entries(spot.actionCounts).map(([action, count]) => (
          <span
            key={action}
            className="count-badge"
            style={{ backgroundColor: ACTION_COLORS[action] || '#374151' }}
          >
            {action}: {count}
          </span>
        ))}
        <span className="count-badge count-total">
          TOTAL: {spot.total}{!isValid && ' ⚠'}
        </span>
      </div>
    </div>
  );
}
