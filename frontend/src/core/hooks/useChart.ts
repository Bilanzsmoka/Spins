import { useState, useEffect } from 'react';
import type { ChartResponse } from '../models/chart.model';
import { getChartByStack } from '../services/chartApi';

export function useChart(situation: string, stack: string) {
  const [chart, setChart] = useState<ChartResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchData() {
      try {
        setLoading(true);
        setError(null);
        const data = await getChartByStack(situation, stack);
        if (!cancelled) {
          setChart(data);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unknown error');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    fetchData();
    return () => { cancelled = true; };
  }, [situation, stack]);

  return { chart, loading, error };
}
