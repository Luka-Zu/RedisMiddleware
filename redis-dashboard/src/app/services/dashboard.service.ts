import { Injectable } from '@angular/core';
import { ChartConfiguration, ChartOptions } from 'chart.js';
import { format, subHours } from 'date-fns';
import { BehaviorSubject } from 'rxjs';
import { ApiService } from './api.service'; //
import { SignalrService } from './signalr.service'; //
import { ServerMetric } from '../interfaces/ServerMetric'; //
import { RequestLog } from '../interfaces/RequestLog'; //
import { getPercentile, shiftChart } from '../utils/chart-utils';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  // --- STATE ---
  public filterDate: string = format(subHours(new Date(), 1), "yyyy-MM-dd'T'HH:mm");
  public currentOpsPerSec = 0;
  public connectedClients = 0;
  public fragmentationRatio = 0;
  public evictedKeys = 0;
  
  public requestLogs: RequestLog[] = [];
  public hotKeys: { key: string; count: number }[] = [];

  // --- CHART DATA (Initialized Here) ---
  public commandChartData: ChartConfiguration<'doughnut'>['data'] = {
    labels: [],
    datasets: [{
      data: [],
      backgroundColor: ['#36a2eb', '#ff6384', '#ffcd56', '#4bc0c0', '#9966ff', '#ff9f40', '#c9cbcf']
    }]
  };

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

  public latencyChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      { data: [], label: 'P50 (Median)', borderColor: '#36a2eb', backgroundColor: 'rgba(54, 162, 235, 0.1)', fill: false, tension: 0.4 },
      { data: [], label: 'P95 (Tail)', borderColor: '#f39c12', borderDash: [5, 5], fill: false, tension: 0.4 },
      { data: [], label: 'P99 (Max Outliers)', borderColor: '#c0392b', fill: false, tension: 0.4 }
    ]
  };

  // Internal calculation state
  private lastCpuSys: number | null = null;
  private lastTimestamp: number | null = null;
  private latencyWindow: number[] = [];
  
  // Subject to notify component when charts need updates
  public chartsUpdate$ = new BehaviorSubject<boolean>(false);
  public pieChartUpdate$ = new BehaviorSubject<boolean>(false);

  constructor(private apiService: ApiService, private signalRService: SignalrService) {}

  public init() {
    this.signalRService.startConnection();
    this.loadHistory();
    this.subscribeToRealtimeEvents();
  }

  public loadHistory() {
    if (!this.filterDate) return;
    const isoString = new Date(this.filterDate).toISOString();

    // 1. Metrics
    this.apiService.getServerHistory(isoString).subscribe((data: ServerMetric[]) => {
      this.resetCharts();
      data.forEach(metric => this.processMetric(metric, false));
      this.chartsUpdate$.next(true);
    });

    // 2. Logs
    this.apiService.getRequestHistory(isoString).subscribe((data: RequestLog[]) => {
      this.requestLogs = data;
      this.latencyWindow = data.map(d => d.latencyMs).slice(0, 200);
    });

    // 3. Command Stats
    this.apiService.getCommandStats(isoString).subscribe(stats => {
      this.commandChartData.labels = stats.map(s => s.command.toUpperCase());
      this.commandChartData.datasets[0].data = stats.map(s => s.count);
      this.pieChartUpdate$.next(true);
    });

    // 4. Hot Keys
    this.apiService.getHotKeys(isoString).subscribe(data => {
      this.hotKeys = data;
    });
  }

  private subscribeToRealtimeEvents() {
    // Metrics
    this.signalRService.serverMetrics$.subscribe((metric: ServerMetric) => {
      this.processMetric(metric, true);
      this.currentOpsPerSec = metric.opsPerSec;
      this.connectedClients = metric.connectedClients;
      this.fragmentationRatio = metric.fragmentationRatio;
      this.evictedKeys = metric.evictedKeys;
      this.chartsUpdate$.next(true);
    });

    // Logs
    this.signalRService.requestLogs$.subscribe((newLogs: RequestLog[]) => {
      // 1. Update Table
      this.requestLogs = [...newLogs, ...this.requestLogs].slice(0, 1000); // Fixed slice limit

      // 2. Update Pie Chart Logic
      let chartChanged = false;
      const labels = this.commandChartData.labels as string[];
      const data = this.commandChartData.datasets[0].data as number[];

      newLogs.forEach(log => {
        const cmd = log.command.toUpperCase();
        const index = labels.indexOf(cmd);
        if (index !== -1) {
          data[index] = Number(data[index]) + 1;
          chartChanged = true;
        } else {
          labels.push(cmd);
          data.push(1);
          chartChanged = true;
        }
      });
      if (chartChanged) this.pieChartUpdate$.next(true);

      // 3. Update Hot Keys
      newLogs.forEach(log => {
        if (!log.key) return;
        const existingItem = this.hotKeys.find(i => i.key === log.key);
        if (existingItem) existingItem.count++;
        else this.hotKeys.push({ key: log.key, count: 1 });
      });
      this.hotKeys.sort((a, b) => b.count - a.count);
      if (this.hotKeys.length > 10) this.hotKeys = this.hotKeys.slice(0, 10);

      // 4. Update Latency Window
      newLogs.forEach(log => this.latencyWindow.push(log.latencyMs));
      if (this.latencyWindow.length > 200) {
        this.latencyWindow = this.latencyWindow.slice(this.latencyWindow.length - 200);
      }
    });
  }

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

    // Push data to chart objects
    this.cpuChartData.labels?.push(timeLabel);
    this.cpuChartData.datasets[0].data.push(cpuPercent);

    const memUsedMb = metric.usedMemory / 1024 / 1024;
    const memRssMb = metric.usedMemoryRss / 1024 / 1024;
    this.memoryChartData.labels?.push(timeLabel);
    this.memoryChartData.datasets[0].data.push(memUsedMb);
    this.memoryChartData.datasets[1].data.push(memRssMb);

    this.netChartData.labels?.push(timeLabel);
    this.netChartData.datasets[0].data.push(metric.inputKbps);
    this.netChartData.datasets[1].data.push(metric.outputKbps);

    this.hitChartData.labels?.push(timeLabel);
    this.hitChartData.datasets[0].data.push(metric.keyspaceHits);
    this.hitChartData.datasets[1].data.push(metric.keyspaceMisses);

    // Latency
    const p50 = getPercentile(this.latencyWindow, 50);
    const p95 = getPercentile(this.latencyWindow, 95);
    const p99 = getPercentile(this.latencyWindow, 99);
    
    this.latencyChartData.labels?.push(timeLabel);
    this.latencyChartData.datasets[0].data.push(p50);
    this.latencyChartData.datasets[1].data.push(p95);
    this.latencyChartData.datasets[2].data.push(p99);

    if (isRealtime && this.cpuChartData.labels && this.cpuChartData.labels.length > 50) {
      shiftChart(this.cpuChartData);
      shiftChart(this.memoryChartData);
      shiftChart(this.netChartData);
      shiftChart(this.hitChartData);
      shiftChart(this.latencyChartData);
    }
  }

  private resetCharts() {
    this.lastCpuSys = null;
    this.lastTimestamp = null;
    const reset = (config: any) => {
      config.labels = [];
      config.datasets.forEach((ds: any) => ds.data = []);
    };
    reset(this.cpuChartData);
    reset(this.memoryChartData);
    reset(this.netChartData);
    reset(this.hitChartData);
    reset(this.latencyChartData);
    this.latencyWindow = [];
  }
}