import { Component, OnInit, OnDestroy, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { Subscription } from 'rxjs';
import { ChartOptions, Chart } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import ChartDataLabels from 'chartjs-plugin-datalabels';
import { DashboardService } from '../../services/dashboard.service';
import { verticalLinePlugin } from '../../utils/chart-utils'; 

// Register Plugins
Chart.register(verticalLinePlugin);
Chart.register(ChartDataLabels);

@Component({
  selector: 'app-server-health',
  templateUrl: './server-health.component.html',
  styleUrls: ['./server-health.component.scss']
})
export class ServerHealthComponent implements OnInit, OnDestroy {

  // --- VIEW CHILDREN ---
  @ViewChildren(BaseChartDirective) charts?: QueryList<BaseChartDirective>;
  @ViewChild('pieChart') pieChart?: BaseChartDirective;

  // --- STATE ---
  private subscriptions: Subscription[] = [];
  private lastActiveIndex: number | null = null;

  // --- MODAL STATE ---
  public isModalOpen = false;
  public modalTitle = '';
  public modalChartData: any = null;
  public modalChartOptions: any = null;
  public modalChartType: any = 'line';
  public modalPlugins: any[] = [];

  // --- CHART OPTIONS ---
  public pieOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'right', labels: { boxWidth: 15, padding: 15 } },
      datalabels: {
        display: true, color: '#ffffff', font: { weight: 'bold', size: 14 },
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
    plugins: { legend: { labels: { boxWidth: 10 } }, tooltip: { enabled: true, animation: false } , datalabels: { display: false }},
    
    onHover: (event, activeElements, chart) => this.syncCharts(event, activeElements, chart)
  };

  constructor(public dashboard: DashboardService) { }

  ngOnInit() {
    // Subscribe to updates from the service
    this.subscriptions.push(
      this.dashboard.chartsUpdate$.subscribe(() => this.updateAllCharts()),
      this.dashboard.pieChartUpdate$.subscribe(() => this.pieChart?.chart?.update())
    );
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  private updateAllCharts() {
    this.charts?.forEach(child => child.update());
  }

  // --- MODAL METHODS ---
  public openChartModal(title: string, data: any, options: any, type: 'line' | 'doughnut' = 'line', plugins: any[] = []) {
    this.modalTitle = title;
    this.modalChartData = data;
    this.modalChartOptions = { ...options, maintainAspectRatio: false, animation: false };
    this.modalChartType = type;
    this.modalPlugins = plugins;
    this.isModalOpen = true;
  }

  public closeModal() {
    this.isModalOpen = false;
    this.modalChartData = null;
  }

  public stopPropagation(event: Event) {
    event.stopPropagation();
  }

  // --- CHART SYNC LOGIC ---
  public chartMouseLeave() {
    this.lastActiveIndex = null;
    this.charts?.forEach(c => {
      const chartInstance = c.chart;
      if (!chartInstance || (chartInstance.config as any).type !== 'line') return;
      if (chartInstance.tooltip) chartInstance.tooltip.setActiveElements([], { x: 0, y: 0 });
      chartInstance.update('none');
    });
  }

  private syncCharts(event: any, activeElements: any[], sourceChart: any) {
    if (!activeElements || activeElements.length === 0) {
      if (this.lastActiveIndex !== null) this.chartMouseLeave();
      return;
    }

    if (sourceChart && sourceChart.canvas && !sourceChart.canvas.matches(':hover')) return;

    const currentActiveIndex = activeElements[0].index;
    if (currentActiveIndex === this.lastActiveIndex) return;
    this.lastActiveIndex = currentActiveIndex;

    this.charts?.forEach(c => {
      const targetChart = c.chart;
      if (!targetChart || targetChart === sourceChart || (targetChart.config as any).type !== 'line') return;

      if (targetChart.data.datasets.length > 0) {
        const newActiveElements = targetChart.data.datasets.map((ds, dsIndex) => ({
          datasetIndex: dsIndex,
          index: currentActiveIndex,
        }));
        if (targetChart.tooltip) targetChart.tooltip.setActiveElements(newActiveElements, { x: 0, y: 0 });
        targetChart.update('none');
      }
    });
  }
} 