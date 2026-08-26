import { ACTION_COLORS, ACTION_LABELS } from '../../../core/constants/poker';

interface ChartCellProps {
  handLabel: string;
  action: string;
}

export function ChartCell({ handLabel, action }: ChartCellProps) {
  const color = ACTION_COLORS[action] || '#374151';
  const label = ACTION_LABELS[action] || action;
  const isPair = handLabel.length === 2 && handLabel[0] === handLabel[1];

  return (
    <div
      className="chart-cell"
      style={{ backgroundColor: color }}
      title={`${handLabel}: ${label}`}
    >
      <span className={`cell-hand${isPair ? ' cell-pair' : ''}`}>
        {handLabel}
      </span>
    </div>
  );
}
