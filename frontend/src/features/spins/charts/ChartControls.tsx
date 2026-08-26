import { Select } from '../../../shared/components/Select';

interface ChartControlsProps {
  situation: string;
  stack: string;
  stacks: string[];
  spots: { value: string; label: string }[];
  onSituationChange: (v: string) => void;
  onStackChange: (v: string) => void;
  onSpotChange: (v: string | null) => void;
}

export function ChartControls({
  situation, stack, stacks, spots,
  onSituationChange, onStackChange, onSpotChange,
}: ChartControlsProps) {
  return (
    <div className="chart-controls">
      <Select label="Situación" value={situation}
        options={[{ value: 'HU_SB_OR_FISH', label: 'HU SB OR | fish' }]}
        onChange={onSituationChange} />
      <Select label="Stack" value={stack}
        options={stacks.map((s) => ({ value: s, label: s }))}
        onChange={onStackChange} />
      <Select label="Spot" value=""
        options={[{ value: '', label: 'Todos los spots' }, ...spots]}
        onChange={(v) => onSpotChange(v || null)} />
    </div>
  );
}
