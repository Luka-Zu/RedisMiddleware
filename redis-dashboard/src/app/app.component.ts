import { Component, OnInit, OnDestroy, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { Subscription } from 'rxjs';
import { ChartConfiguration, ChartOptions, Chart } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ApiService } from './services/api.service';
import { SignalrService } from './services/signalr.service';
import { format, subHours } from 'date-fns';
import { RequestLog } from './interfaces/RequestLog';
import { ServerMetric } from './interfaces/ServerMetric';
import ChartDataLabels from 'chartjs-plugin-datalabels';


// --- 1. DEFINE CUSTOM PLUGINS ---

const verticalLinePlugin = {
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

// --- 2. REGISTER PLUGINS GLOBALLY ---
Chart.register(verticalLinePlugin);
Chart.register(ChartDataLabels);

// --- 3. CONFIGURE DEFAULTS ---
Chart.defaults.set('plugins.datalabels', {
  display: false
});

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  private subscriptions: Subscription[] = [];
  public isModalOpen = false;
  public modalTitle = '';
  public modalChartData: any = null;
  public modalChartOptions: any = null;
  public modalChartType: 'line' | 'doughnut' = 'line';
  public modalPlugins: any[] = [];

  private lastActiveIndex: number | null = null;
  private syncFrameId: number | null = null;

  // Method to open the modal
  public openChartModal(title: string, data: any, options: any, type: 'line' | 'doughnut' = 'line', plugins: any[] = []) {
    this.modalTitle = title;
    this.modalChartData = data;
    // We clone options to ensure the modal can have different layout settings if needed (like aspect ratio)
    this.modalChartOptions = {
      ...options,
      maintainAspectRatio: false, // Allow modal to fill screen
      animation: false
    };
    this.modalChartType = type;
    this.modalPlugins = plugins;
    this.isModalOpen = true;
  }

  public closeModal() {
    this.isModalOpen = false;
    this.modalChartData = null; // Clear reference
  }

  public stopPropagation(event: Event) {
    event.stopPropagation();
  }

  public pieChartPlugins = [ChartDataLabels];

  public hotKeys: { key: string; count: number }[] = [];

  // --- CHART DATA CONFIGURATION ---
  public commandChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: [],
    datasets: [{
      data: [],
      backgroundColor: [
        '#36a2eb', '#ff6384', '#ffcd56', '#4bc0c0', '#9966ff', '#ff9f40', '#c9cbcf'
      ]
    }]
  };

  public pieOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'right', labels: { boxWidth: 15, padding: 15 } },
      datalabels: {
        display: true,
        color: '#ffffff',
        font: { weight: 'bold', size: 14 },
        formatter: (value) => value === 0 ? '' : value
      }
    }
  };

  public commonOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    elements: { point: { radius: 0 } },
    scales: { x: { display: false }, y: { beginAtZero: true } },
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { labels: { boxWidth: 10 } }, tooltip: { enabled: true, animation: false } },
    onHover: (event, activeElements, chart) => this.syncCharts(event, activeElements, chart)
  };

  // --- STATE VARIABLES ---
  private lastCpuSys: number | null = null;
  private lastTimestamp: number | null = null;

  public filterDate: string = format(subHours(new Date(), 1), "yyyy-MM-dd'T'HH:mm");

  public currentOpsPerSec: number = 0;
  public connectedClients: number = 0;
  public fragmentationRatio: number = 0;
  public evictedKeys: number = 0;
  public requestLogs: RequestLog[] = [];

  public latencyChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'P50 (Median)',
        borderColor: '#36a2eb', // Blue
        backgroundColor: 'rgba(54, 162, 235, 0.1)',
        fill: false,
        tension: 0.4
      },
      {
        data: [],
        label: 'P95 (Tail)',
        borderColor: '#f39c12', // Orange
        borderDash: [5, 5],
        fill: false,
        tension: 0.4
      },
      {
        data: [],
        label: 'P99 (Max Outliers)',
        borderColor: '#c0392b', // Red
        fill: false,
        tension: 0.4
      }
    ]
  };

  private latencyWindow: number[] = [];


  @ViewChildren(BaseChartDirective) charts?: QueryList<BaseChartDirective>;
  @ViewChild('pieChart') pieChart?: BaseChartDirective;

  // --- LINE CHARTS ---
  public cpuChartData: ChartConfiguration<'line'>['data'] = {
    labels: [], datasets: [{ data: [], label: 'CPU Usage (%)', borderColor: 'red', backgroundColor: 'rgba(255,0,0,0.2)', fill: true, tension: 0.4 }]
  };
  public memoryChartData: ChartConfiguration<'line'>['data'] = {
    labels: [], datasets: [
      { data: [], label: 'Used Memory (MB)', borderColor: '#36a2eb', backgroundColor: 'rgba(54, 162, 235, 0.1)', fill: true },
      { data: [], label: 'RSS Memory (MB)', borderColor: '#ff6384', borderDash: [5, 5], fill: false }
    ]
  };
  public netChartData: ChartConfiguration<'line'>['data'] = {
    labels: [], datasets: [
      { data: [], label: 'Input (KB/s)', borderColor: '#4bc0c0', fill: false },
      { data: [], label: 'Output (KB/s)', borderColor: '#9966ff', fill: false }
    ]
  };
  public hitChartData: ChartConfiguration<'line'>['data'] = {
    labels: [], datasets: [
      { data: [], label: 'Hits', borderColor: 'green', backgroundColor: 'rgba(0, 255, 0, 0.1)', fill: true },
      { data: [], label: 'Misses', borderColor: 'orange', fill: false }
    ]
  };

  constructor(private apiService: ApiService, private signalRService: SignalrService) { }

  ngOnInit() {
    this.signalRService.startConnection();
    this.loadHistory();
    this.subscribeToRealtimeEvents();
  }

  ngOnDestroy() {
    // Unsubscribe to prevent memory leaks and "ghost" updates
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  // --- DATA LOADING ---

  public loadHistory() {
    if (!this.filterDate) return;

    const isoString = new Date(this.filterDate).toISOString();

    // 1. Metrics
    this.apiService.getServerHistory(isoString).subscribe((data: ServerMetric[]) => {
      this.resetCharts();
      data.forEach(metric => this.processMetric(metric, false));
      this.updateAllCharts();
    });

    // 2. Logs
    this.apiService.getRequestHistory(isoString).subscribe((data: RequestLog[]) => {
      this.requestLogs = data;
    });

    // 3. Command Stats (Pie Chart)
    this.apiService.getCommandStats(isoString).subscribe(stats => {
      // Update the data object
      this.commandChartData.labels = stats.map(s => s.command.toUpperCase());
      this.commandChartData.datasets[0].data = stats.map(s => s.count);

      // Manually tell the chart to redraw
      this.pieChart?.chart?.update();
    });

    // 4. Load Hot Keys
    this.apiService.getHotKeys(isoString).subscribe(data => {
      this.hotKeys = data;
    });

    this.apiService.getRequestHistory(isoString).subscribe((data: RequestLog[]) => {
      this.requestLogs = data;

      // Initialize buffer with historical data
      this.latencyWindow = data.map(d => d.latencyMs).slice(0, 200);
    });
  }

  public onFilterChange() {
    this.loadHistory();
  }

  // --- REAL-TIME UPDATES ---

  private subscribeToRealtimeEvents() {
    // 1. Server Metrics
    const metricSub = this.signalRService.serverMetrics$.subscribe((metric: ServerMetric) => {
      this.processMetric(metric, true);
      this.updateAllCharts();
      this.currentOpsPerSec = metric.opsPerSec;
      this.connectedClients = metric.connectedClients;
      this.fragmentationRatio = metric.fragmentationRatio;
      this.evictedKeys = metric.evictedKeys;
    });

    this.subscriptions.push(metricSub);

    // 2. Request Logs
    const logSub = this.signalRService.requestLogs$.subscribe((newLogs: RequestLog[]) => {
      // Update Table
      this.requestLogs = [...newLogs, ...this.requestLogs].slice(0, 10000);

      let chartChanged = false;
      const labels = this.commandChartData.labels as string[];
      const data = this.commandChartData.datasets[0].data as number[];


      newLogs.forEach(log => {
        const cmd = log.command.toUpperCase();
        const index = labels.indexOf(cmd);

        if (index !== -1) {
          // Increment existing
          data[index] = Number(data[index]) + 1;
          chartChanged = true;
        } else {
          // Add new
          labels.push(cmd);
          data.push(1);
          chartChanged = true;
        }
      });

      if (chartChanged) {
        this.pieChart?.chart?.update();
      }

      newLogs.forEach(log => {
        if (!log.key) return; // Skip logs without keys (like PING)

        const existingItem = this.hotKeys.find(i => i.key === log.key);

        if (existingItem) {
          existingItem.count++;
        } else {
          this.hotKeys.push({ key: log.key, count: 1 });
        }
      });

      this.hotKeys.sort((a, b) => b.count - a.count);

      if (this.hotKeys.length > 10) {
        this.hotKeys = this.hotKeys.slice(0, 10);
      }

      newLogs.forEach(log => {
        this.latencyWindow.push(log.latencyMs);
      });

      // Keep buffer size manageable (e.g., last 200 requests)
      // This represents the "Window" we are analyzing
      if (this.latencyWindow.length > 200) {
        this.latencyWindow = this.latencyWindow.slice(this.latencyWindow.length - 200);
      }
    }
    );
  }

  // --- HELPER LOGIC ---

  private processMetric(metric: ServerMetric, isRealtime: boolean) {
    const timeLabel = format(new Date(metric.timestamp), 'HH:mm:ss');
    const currentTime = new Date(metric.timestamp).getTime();

    let cpuPercent = 0;
    if (this.lastCpuSys !== null && this.lastTimestamp !== null) {
      const cpuDelta = metric.usedCpuSys - this.lastCpuSys;
      const timeDeltaSeconds = (currentTime - this.lastTimestamp) / 1000;
      if (timeDeltaSeconds > 0) cpuPercent = (cpuDelta / timeDeltaSeconds) * 100;
    }
    this.lastCpuSys = metric.usedCpuSys;
    this.lastTimestamp = currentTime;

    const memUsedMb = metric.usedMemory / 1024 / 1024;
    const memRssMb = metric.usedMemoryRss / 1024 / 1024;

    this.cpuChartData.labels?.push(timeLabel);
    this.cpuChartData.datasets[0].data.push(cpuPercent);

    this.memoryChartData.labels?.push(timeLabel);
    this.memoryChartData.datasets[0].data.push(memUsedMb);
    this.memoryChartData.datasets[1].data.push(memRssMb);

    this.netChartData.labels?.push(timeLabel);
    this.netChartData.datasets[0].data.push(metric.inputKbps);
    this.netChartData.datasets[1].data.push(metric.outputKbps);

    this.hitChartData.labels?.push(timeLabel);
    this.hitChartData.datasets[0].data.push(metric.keyspaceHits);
    this.hitChartData.datasets[1].data.push(metric.keyspaceMisses);

    if (isRealtime && this.cpuChartData.labels && this.cpuChartData.labels.length > 50) {
      this.shiftChart(this.cpuChartData);
      this.shiftChart(this.memoryChartData);
      this.shiftChart(this.netChartData);
      this.shiftChart(this.hitChartData);
    }

    // 1. Calculate P50, P95, P99 from our sliding window
    const p50 = this.getPercentile(this.latencyWindow, 50);
    const p95 = this.getPercentile(this.latencyWindow, 95);
    const p99 = this.getPercentile(this.latencyWindow, 99);

    // 2. Push to Chart
    this.latencyChartData.labels?.push(timeLabel);
    this.latencyChartData.datasets[0].data.push(p50);
    this.latencyChartData.datasets[1].data.push(p95);
    this.latencyChartData.datasets[2].data.push(p99);

    // 3. Cleanup: Limit chart width
    if (isRealtime && this.latencyChartData.labels && this.latencyChartData.labels.length > 50) {
      this.shiftChart(this.latencyChartData);
    }
  }

  private shiftChart(config: ChartConfiguration['data']) {
    config.labels?.shift();
    config.datasets.forEach(ds => ds.data.shift());
  }

  private resetCharts() {
    this.lastCpuSys = null;
    this.lastTimestamp = null;
    const reset = (config: ChartConfiguration['data']) => {
      config.labels = [];
      config.datasets.forEach(ds => ds.data = []);
    };
    reset(this.cpuChartData);
    reset(this.memoryChartData);
    reset(this.netChartData);
    reset(this.hitChartData);
    reset(this.latencyChartData);

    // Clear buffer
    this.latencyWindow = [];
  }

  private updateAllCharts() {
    this.charts?.forEach(child => child.update());
  }



  public chartMouseLeave() {
    this.lastActiveIndex = null;

    this.charts?.forEach(c => {
      const chartInstance = c.chart;
      
      if (!chartInstance || (chartInstance.config as any).type !== 'line') return;

      if (chartInstance.tooltip) {
        chartInstance.tooltip.setActiveElements([], { x: 0, y: 0 });
      }
      
      chartInstance.update('none');
    });
  }

  private syncCharts(event: any, activeElements: any[], sourceChart: any) {
    if (sourceChart && sourceChart.canvas && !sourceChart.canvas.matches(':hover')) {
      return;
    }

    if (!activeElements || activeElements.length === 0) {
      if (this.lastActiveIndex !== null) {
        this.chartMouseLeave();
      }
      return;
    }

    const currentActiveIndex = activeElements[0].index;

    if (currentActiveIndex === this.lastActiveIndex) {
      return; 
    }
    
    this.lastActiveIndex = currentActiveIndex;

    this.charts?.forEach(c => {
      const targetChart = c.chart;

      if (!targetChart || targetChart === sourceChart || (targetChart.config as any).type !== 'line') {
        return;
      }

      if (targetChart.data.datasets.length > 0) {
        const newActiveElements = targetChart.data.datasets.map((ds, dsIndex) => ({
          datasetIndex: dsIndex,
          index: currentActiveIndex,
        }));

        if (targetChart.tooltip) {
           targetChart.tooltip.setActiveElements(newActiveElements, { x: 0, y: 0 });
        }
        targetChart.update('none');
      }
    });
  }

  private getPercentile(data: number[], percentile: number): number {
    if (data.length === 0) return 0;

    // Sort numbers ascending
    const sorted = [...data].sort((a, b) => a - b);

    // Calculate index
    const index = Math.ceil((percentile / 100) * sorted.length) - 1;
    return sorted[Math.max(0, index)];
  }

}