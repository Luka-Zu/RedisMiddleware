import { Component, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { ChartConfiguration, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ApiService } from './services/api.service';
import { SignalrService } from './services/signalr.service';
import { format, subHours } from 'date-fns';
import { RequestLog } from './interfaces/RequestLog';
import { ServerMetric } from './interfaces/ServerMetric';
import { Chart } from 'chart.js';

const verticalLinePlugin = {
  id: 'verticalLine',
  afterDraw: (chart: any) => {
    // Only draw if there is an active tooltip (user is hovering)
    if (chart.tooltip?._active?.length) {
      const activePoint = chart.tooltip._active[0];
      const ctx = chart.ctx;
      const x = activePoint.element.x;
      const top = chart.scales.y.top;
      const bottom = chart.scales.y.bottom;

      // Draw the line
      ctx.save();
      ctx.beginPath();
      ctx.moveTo(x, top);
      ctx.lineTo(x, bottom);
      ctx.lineWidth = 1;
      ctx.strokeStyle = 'rgba(0,0,0, 0.5)'; // Dark grey line
      ctx.setLineDash([3, 3]); // Dashed line
      ctx.stroke();
      ctx.restore();
    }
  }
};

Chart.register(verticalLinePlugin);



@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  public commandChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        backgroundColor: [
          '#36a2eb', // Blue
          '#ff6384', // Red
          '#ffcd56', // Yellow
          '#4bc0c0', // Teal
          '#9966ff', // Purple
        ]
      }
    ]
  };

  public pieOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    plugins: {
      legend: { position: 'right' }
    }
  };

  // Shared Options for clean look
  public commonOptions: ChartOptions<'line'> = {
    responsive: true,
    animation: false,
    elements: { point: { radius: 0 } },
    scales: {
      x: { display: false },
      // Ensure Y axis has some padding so lines don't get cut off
      y: { beginAtZero: true }
    },
    interaction: {
      mode: 'index', // Important: highlight all items at this index
      intersect: false,
    },
    plugins: {
      legend: { labels: { boxWidth: 10 } },
      tooltip: {
        enabled: true, // We will manually trigger them, but keep enabled
        animation: false // Instant tooltips for performance
      }
    },
    // --- MAGIC HAPPENS HERE ---
    onHover: (event, activeElements, chart) => {
      this.syncCharts(event, activeElements, chart);
    }
  };

  private syncCharts(event: any, activeElements: any[], sourceChart: any) {
    // 1. If not hovering over a point, hide tooltips on all charts
    if (!activeElements || activeElements.length === 0) {
      this.charts?.forEach(c => {
        const chartInstance = c.chart;
        if (chartInstance && chartInstance !== sourceChart) {
          chartInstance.tooltip?.setActiveElements([], { x: 0, y: 0 });
          chartInstance.update();
        }
      });
      return;
    }

    // 2. Get the index of the data point being hovered
    const activeIndex = activeElements[0].index;

    // 3. Loop through all OTHER charts and activate the same index
    this.charts?.forEach(c => {
      const targetChart = c.chart;

      // Skip the chart that triggered the event (it handles itself)
      if (!targetChart || targetChart === sourceChart) return;

      // Find the elements at the same index in the target chart
      // We assume dataset 0 is always present
      if (targetChart.data.datasets.length > 0) {

        // Construct an array of active elements for this chart
        // We want to highlight ALL datasets at this index
        const newActiveElements = targetChart.data.datasets.map((ds, dsIndex) => ({
          datasetIndex: dsIndex,
          index: activeIndex,
        }));

        // Manually trigger the tooltip
        targetChart.tooltip?.setActiveElements(newActiveElements, { x: 0, y: 0 });
        targetChart.update();
      }
    });
  }

  // --- STATE VARIABLES ---
  private lastCpuSys: number | null = null;
  private lastTimestamp: number | null = null;

  // Default filter: 1 hour ago
  public filterDate: string = format(subHours(new Date(), 1), "yyyy-MM-dd'T'HH:mm");

  // KPI Card Data
  public currentOpsPerSec: number = 0;
  public connectedClients: number = 0;
  public fragmentationRatio: number = 0;
  public evictedKeys: number = 0;

  // Table Data
  public requestLogs: RequestLog[] = [];

  // Access all charts in the view to update them individually
  @ViewChildren(BaseChartDirective) charts?: QueryList<BaseChartDirective>;

  // --- CHART CONFIGURATIONS ---

  // 1. CPU Usage Chart
  public cpuChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'CPU Usage (%)',
        borderColor: 'red',
        backgroundColor: 'rgba(255,0,0,0.2)',
        fill: true,
        tension: 0.4
      }
    ]
  };

  // 2. Memory Analysis Chart (Used vs RSS)
  public memoryChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'Used Memory (MB)',
        borderColor: '#36a2eb',
        backgroundColor: 'rgba(54, 162, 235, 0.1)',
        fill: true
      },
      {
        data: [],
        label: 'RSS Memory (MB)',
        borderColor: '#ff6384',
        borderDash: [5, 5],
        fill: false
      }
    ]
  };

  // 3. Network Traffic Chart (I/O)
  public netChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      { data: [], label: 'Input (KB/s)', borderColor: '#4bc0c0', fill: false },
      { data: [], label: 'Output (KB/s)', borderColor: '#9966ff', fill: false }
    ]
  };

  // 4. Cache Efficiency Chart (Hits vs Misses)
  public hitChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      { data: [], label: 'Hits', borderColor: 'green', backgroundColor: 'rgba(0, 255, 0, 0.1)', fill: true },
      { data: [], label: 'Misses', borderColor: 'orange', fill: false }
    ]
  };


  constructor(
    private apiService: ApiService,
    private signalRService: SignalrService
  ) { }

  ngOnInit() {
    this.signalRService.startConnection();
    this.loadHistory();
    this.subscribeToRealtimeEvents();
  }

  // --- DATA LOADING ---

  public loadHistory() {
    if (!this.filterDate) return;

    const isoString = new Date(this.filterDate).toISOString();
    console.log("Fetching history from:", isoString);

    // 1. Load Metrics
    this.apiService.getServerHistory(isoString).subscribe((data: ServerMetric[]) => {
      this.resetCharts();

      // Process all history without limiting/shifting
      data.forEach(metric => {
        this.processMetric(metric, false); // false = do not shift old data
      });

      this.updateAllCharts();
    });

    // 2. Load Logs
    this.apiService.getRequestHistory(isoString).subscribe((data: RequestLog[]) => {
      this.requestLogs = data;
    });

    this.apiService.getCommandStats(isoString).subscribe(stats => {
      // stats is array of { command: string, count: number }

      this.commandChartData.labels = stats.map(s => s.command);
      this.commandChartData.datasets[0].data = stats.map(s => s.count);

      // Update the specific chart if you have a ViewChild for it, 
      // or just update all
      this.charts?.forEach(c => c.update());
    });
  }

  public onFilterChange() {
    this.loadHistory();
  }

  // --- REAL-TIME UPDATES ---

  private subscribeToRealtimeEvents() {
    this.signalRService.serverMetrics$.subscribe((metric: ServerMetric) => {
      // Process new metric AND shift old data (limit window size)
      this.processMetric(metric, true);
      this.updateAllCharts();

      // Update KPIs
      this.currentOpsPerSec = metric.opsPerSec;
      this.connectedClients = metric.connectedClients;
      this.fragmentationRatio = metric.fragmentationRatio;
      this.evictedKeys = metric.evictedKeys;
    });

    this.signalRService.requestLogs$.subscribe((newLogs: RequestLog[]) => {
      // Add new logs to top, keep 50
      this.requestLogs = [...newLogs, ...this.requestLogs].slice(0, 50);

      newLogs.forEach(log => {
      const cmd = log.command;
      const labels = this.commandChartData.labels as string[];
      const data = this.commandChartData.datasets[0].data as number[];

      const index = labels.indexOf(cmd);

      if (index !== -1) {
        // Command exists in chart, increment count
        data[index]++;
      } else {
        // New command found! 
        // Only add if we have less than 5 slices, or replace the smallest one?
        // For simplicity, let's just push it (chart handles resizing)
        labels.push(cmd);
        data.push(1);
      }
    });

    // Force chart update
    this.charts?.forEach(c => c.update());
    });
  }

  // --- HELPER LOGIC ---

  private processMetric(metric: ServerMetric, isRealtime: boolean) {
    const timeLabel = format(new Date(metric.timestamp), 'HH:mm:ss');

    // -- 1. Calculate CPU % Delta --
    const currentCpu = metric.usedCpuSys;
    const currentTime = new Date(metric.timestamp).getTime();
    let cpuPercent = 0;

    if (this.lastCpuSys !== null && this.lastTimestamp !== null) {
      const cpuDelta = currentCpu - this.lastCpuSys;
      const timeDeltaSeconds = (currentTime - this.lastTimestamp) / 1000;
      if (timeDeltaSeconds > 0) {
        cpuPercent = (cpuDelta / timeDeltaSeconds) * 100;
      }
    }
    // Update trackers
    this.lastCpuSys = currentCpu;
    this.lastTimestamp = currentTime;

    // -- 2. Calculate Memory MB --
    const memUsedMb = metric.usedMemory / 1024 / 1024;
    const memRssMb = metric.usedMemoryRss / 1024 / 1024;

    // -- 3. Push to Data Arrays --

    // CPU Chart
    this.cpuChartData.labels?.push(timeLabel);
    this.cpuChartData.datasets[0].data.push(cpuPercent);

    // Memory Chart
    this.memoryChartData.labels?.push(timeLabel);
    this.memoryChartData.datasets[0].data.push(memUsedMb);
    this.memoryChartData.datasets[1].data.push(memRssMb);

    // Network Chart
    this.netChartData.labels?.push(timeLabel);
    this.netChartData.datasets[0].data.push(metric.inputKbps);
    this.netChartData.datasets[1].data.push(metric.outputKbps);

    // Hits/Misses Chart
    this.hitChartData.labels?.push(timeLabel);
    this.hitChartData.datasets[0].data.push(metric.keyspaceHits);
    this.hitChartData.datasets[1].data.push(metric.keyspaceMisses);

    // -- 4. Limit Array Size (Only in Realtime Mode) --
    if (isRealtime) {
      const MAX_POINTS = 50;
      // We check one chart; if it's over limit, we shift all of them
      if (this.cpuChartData.labels && this.cpuChartData.labels.length > MAX_POINTS) {
        this.shiftChart(this.cpuChartData);
        this.shiftChart(this.memoryChartData);
        this.shiftChart(this.netChartData);
        this.shiftChart(this.hitChartData);
      }
    }
  }

  // Removes the oldest data point to create the "scrolling" effect
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
  }

  private updateAllCharts() {
    this.charts?.forEach(child => child.update());
  }
}