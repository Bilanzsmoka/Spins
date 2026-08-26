import type { ChartResponse } from '../models/chart.model';

const API_BASE = '/api';

export async function getChartByStack(situation: string, stack: string): Promise<ChartResponse> {
  const response = await fetch(`${API_BASE}/charts/${situation}/${stack}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch chart: ${response.statusText}`);
  }
  return response.json();
}
