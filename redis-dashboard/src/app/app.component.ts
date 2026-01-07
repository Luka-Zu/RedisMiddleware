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

  public filterDate: string = format(subHours(new Date(), 1), "yyyy-MM-dd'T'HH:mm");

  // --- CHART CONFIG ---
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
    animation: false, // Disable animation for performance on real-time updates
    elements: { point: { radius: 0 } } // Hide points to look cleaner
  };

  // --- TABLE DATA ---
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

    // 1. Create a Date object from the input
    const dateObj = new Date(this.filterDate);
    // 2. Convert to ISO string (UTC) for the backend
    const isoString = dateObj.toISOString();

    console.log("Fetching data since:", isoString);

    // 3. Load Server Stats (Chart)
    this.apiService.getServerHistory(isoString).subscribe(data => {
  
      // RESET everything
      this.lineChartData.labels = [];
      this.lineChartData.datasets[0].data = [];
      this.lineChartData.datasets[1].data = [];
      this.lastCpuSys = null; // Reset tracker
      this.lastTimestamp = null;

      data.forEach(metric => {
        this.updateChart(metric); 
      });
      
      this.chart?.update();
    });

    // 4. Load Request Logs (Table)
    this.apiService.getRequestHistory(isoString).subscribe(data => {
      console.log("Received logs:", data.length);
      this.requestLogs = data;
    });
  }

  // 3. EVENT HANDLER
  public onFilterChange() {
    this.loadHistory();
  }

  private subscribeToRealtimeEvents() {
    // SignalR: Server Update (Chart)
    this.signalRService.serverMetrics$.subscribe(metric => {
      this.updateChart(metric);
      this.currentOpsPerSec = metric.opsPerSec;
      this.connectedClients = metric.connectedClients;
    });

    // SignalR: New Requests (Table)
    this.signalRService.requestLogs$.subscribe(newLogs => {
      // Add new logs to the top and keep list size at 50
      this.requestLogs = [...newLogs, ...this.requestLogs].slice(0, 50);
    });
  }

  private updateChart(metric: any) {
    const timeLabel = format(new Date(metric.timestamp), 'HH:mm:ss');
    const memoryMb = metric.usedMemory / 1024 / 1024;
    const currentCpu = metric.usedCpuSys;
    const currentTime = new Date(metric.timestamp).getTime();

    let cpuPercent = 0;

    // CALCULATE DELTA (CPU Usage %)
    if (this.lastCpuSys !== null && this.lastTimestamp !== null) {
      const cpuDelta = currentCpu - this.lastCpuSys;
      const timeDeltaSeconds = (currentTime - this.lastTimestamp) / 1000;

      if (timeDeltaSeconds > 0) {
        // (CPU Seconds Used / Wall Clock Seconds) * 100 = Percentage
        cpuPercent = (cpuDelta / timeDeltaSeconds) * 100;
      }
    }

    // Update "Last" values for the next loop
    this.lastCpuSys = currentCpu;
    this.lastTimestamp = currentTime;

    // Add to Chart
    this.lineChartData.labels?.push(timeLabel);
    this.lineChartData.datasets[0].data.push(memoryMb);
    this.lineChartData.datasets[1].data.push(cpuPercent); // Graph the % now!

    // Shift old data if needed
    if (this.lineChartData.labels && this.lineChartData.labels.length > 50) {
      this.lineChartData.labels.shift();
      this.lineChartData.datasets[0].data.shift();
      this.lineChartData.datasets[1].data.shift();
    }
    
    // Update the real-time chart reference if it exists
    if (this.chart) {
        this.chart.update();
    }
  }
}