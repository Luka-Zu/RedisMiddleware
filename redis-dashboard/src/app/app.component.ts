import { Component, OnInit, ViewChild } from '@angular/core';
import { ChartConfiguration, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { ApiService } from './services/api.service';
import { SignalrService } from './services/signalr.service';
import { format, subHours } from 'date-fns';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  private lastCpuSys: number | null = null;
  private lastTimestamp: number | null = null;

  // Initial filter: 1 hour ago
  public filterDate: string = format(subHours(new Date(), 1), "yyyy-MM-dd'T'HH:mm");

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  public lineChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'Memory Usage (MB)',
        borderColor: 'rgba(148,159,177,1)',
        backgroundColor: 'rgba(148,159,177,0.2)',
        fill: true,
      },
      {
        data: [],
        label: 'CPU Usage (System)',
        borderColor: 'red',
        backgroundColor: 'rgba(255,0,0,0.3)',
        fill: false
      }
    ]
  };

  public lineChartOptions: ChartOptions<'line'> = {
    responsive: true,
    animation: false,
    elements: { point: { radius: 0 } }
  };

  public requestLogs: any[] = [];
  public currentOpsPerSec: number = 0;
  public connectedClients: number = 0;

  constructor(
    private apiService: ApiService, 
    private signalRService: SignalrService
  ) {}

  ngOnInit() {
    this.signalRService.startConnection();
    this.loadHistory();
    this.subscribeToRealtimeEvents();
  }

  public loadHistory() {
    if (!this.filterDate) return;

    const dateObj = new Date(this.filterDate);
    const isoString = dateObj.toISOString();

    console.log("Fetching data since:", isoString);

    this.apiService.getServerHistory(isoString).subscribe(data => {
      // 1. CLEAR existing data
      this.lineChartData.labels = [];
      this.lineChartData.datasets[0].data = [];
      this.lineChartData.datasets[1].data = [];
      this.lastCpuSys = null; 
      this.lastTimestamp = null;

      // 2. PROCESS all history WITHOUT shifting/deleting
      data.forEach(metric => {
        const processed = this.calculateMetricValues(metric);
        
        // Push to arrays directly without size checks
        this.lineChartData.labels?.push(processed.timeLabel);
        this.lineChartData.datasets[0].data.push(processed.memoryMb);
        this.lineChartData.datasets[1].data.push(processed.cpuPercent);
      });
      
      this.chart?.update();
    });

    this.apiService.getRequestHistory(isoString).subscribe(data => {
      this.requestLogs = data;
    });
  }

  public onFilterChange() {
    this.loadHistory();
  }

  private subscribeToRealtimeEvents() {
    this.signalRService.serverMetrics$.subscribe(metric => {
      // For Real-time: Use the method that includes the 'shift' logic
      this.addRealtimeMetric(metric);
      
      this.currentOpsPerSec = metric.opsPerSec;
      this.connectedClients = metric.connectedClients;
    });

    this.signalRService.requestLogs$.subscribe(newLogs => {
      this.requestLogs = [...newLogs, ...this.requestLogs].slice(0, 50);
    });
  }

  // Helper to extract math logic 
  private calculateMetricValues(metric: any) {
    const timeLabel = format(new Date(metric.timestamp), 'HH:mm:ss');
    const memoryMb = metric.usedMemory / 1024 / 1024;
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

    return { timeLabel, memoryMb, cpuPercent };
  }

  // Logic specifically for Real-time updates (sliding window)
  private addRealtimeMetric(metric: any) {
    const val = this.calculateMetricValues(metric);

    this.lineChartData.labels?.push(val.timeLabel);
    this.lineChartData.datasets[0].data.push(val.memoryMb);
    this.lineChartData.datasets[1].data.push(val.cpuPercent);

    // LIMIT LOGIC: Only apply this when a new live event comes in
    // If we just loaded history, we might have 1000 points. 
    // We can either let it grow or maintain a larger buffer.
    // Here we ensure it doesn't grow infinitely, but respects the history load.
    
    const MAX_POINTS = 50; 
    
    // NOTE: If you just loaded history (e.g. 500 points), and a new point comes in, 
    // this check will trigger. If you want to keep the history visible, 
    // you should remove this check or increase MAX_POINTS significantly.
    // If you want a "sliding window" of 50 always, then keep it, 
    // but the history load will slowly disappear again point by point.
    
    // Suggestion: Allow chart to be larger if history is loaded
    if (this.lineChartData.labels && this.lineChartData.labels.length > 2000) { 
       this.lineChartData.labels.shift();
       this.lineChartData.datasets[0].data.shift();
       this.lineChartData.datasets[1].data.shift();
    }

    this.chart?.update();
  }
}