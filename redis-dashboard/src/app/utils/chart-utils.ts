import { Chart } from 'chart.js';

// --- CUSTOM PLUGIN ---
export const verticalLinePlugin = {
  id: 'verticalLine',
  afterDraw: (chart: any) => {
    if (chart.tooltip?._active?.length && chart.scales.y) {
      const activePoint = chart.tooltip._active[0];
      const ctx = chart.ctx;
      const x = activePoint.element.x;
      const top = chart.scales.y.top;
      const bottom = chart.scales.y.bottom;

      ctx.save();
      ctx.beginPath();
      ctx.moveTo(x, top);
      ctx.lineTo(x, bottom);
      ctx.lineWidth = 1;
      ctx.strokeStyle = 'rgba(0,0,0, 0.5)';
      ctx.setLineDash([3, 3]);
      ctx.stroke();
      ctx.restore();
    }
  }
};

// --- HELPER FUNCTIONS ---
export function getPercentile(data: number[], percentile: number): number {
  if (data.length === 0) return 0;
  const sorted = [...data].sort((a, b) => a - b);
  const index = Math.ceil((percentile / 100) * sorted.length) - 1;
  return sorted[Math.max(0, index)];
}

export function shiftChart(config: any) {
  config.labels?.shift();
  config.datasets.forEach((ds: any) => ds.data.shift());
}