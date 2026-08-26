import { ACTION_COLORS } from '../../../core/constants/poker';

export function Legend() {
  return (
    <div className="legend">
      {Object.entries(ACTION_COLORS).map(([action, color]) => (
        <div key={action} className="legend-item">
          <div className="legend-color" style={{ backgroundColor: color }} />
          <span>{action === 'RAISE_X2' ? 'RAISE X2' : action}</span>
        </div>
      ))}
    </div>
  );
}
