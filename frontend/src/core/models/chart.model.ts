export interface ChartResponse {
  situationKey: string;
  situationLabel: string;
  stackKey: string;
  spots: SpotResponse[];
}

export interface SpotResponse {
  spotKey: string;
  spotLabel: string;
  hands: HandAction[];
  actionCounts: Record<string, number>;
  total: number;
}

export interface HandAction {
  handLabel: string;
  action: 'ALL-IN' | 'CALL' | 'FOLD' | 'RAISE_X2';
}
