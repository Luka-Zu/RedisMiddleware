import { Component, OnInit, OnDestroy, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { Subscription } from 'rxjs';
import { ChartOptions, Chart } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import ChartDataLabels from 'chartjs-plugin-datalabels';
import { DashboardService } from './services/dashboard.service'; // Import new service
import { verticalLinePlugin } from './utils/chart-utils'; // Import utils

// Register Plugins
Chart.register(verticalLinePlugin);
Chart.register(ChartDataLabels);
Chart.defaults.set('plugins.datalabels', { display: false });

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  
  // --- UI STATE ---
  public isModalOpen = false;
  public modalTitle = '';
  public modalChartData: any = null;
  public modalChartOptions: any = null;
  public modalChartType: 'line' | 'doughnut' = 'line';
  public modalPlugins: any[] = [];
  public pieChartPlugins = [ChartDataLabels];

  // --- CHART SYNC STATE ---
  private lastActiveIndex: number | null = null;
  
  // --- SUBSCRIPTIONS ---
  private subscriptions: Subscription[] = [];

  // --- VIEW CHILDREN ---
  @ViewChildren(BaseChartDirective) charts?: QueryList<BaseChartDirective>;
  @ViewChild('pieChart') pieChart?: BaseChartDirective;

  // --- CHART OPTIONS (View Logic) ---
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
    plugins: { legend: { labels: { boxWidth: 10 } }, tooltip: { enabled: true, animation: false } },
    onHover: (event, activeElements, chart) => this.syncCharts(event, activeElements, chart)
  };

  constructor(public dashboard: DashboardService) { } // Inject Service as Public

  ngOnInit() {
    this.dashboard.init();

    // Subscribe to chart updates triggered by the service
    this.subscriptions.push(
      this.dashboard.chartsUpdate$.subscribe(() => this.updateAllCharts()),
      this.dashboard.pieChartUpdate$.subscribe(() => this.pieChart?.chart?.update())
    );
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  // --- UI METHODS ---
  public onFilterChange() {
    this.dashboard.loadHistory();
  }

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

  private updateAllCharts() {
    this.charts?.forEach(child => child.update());
  }

  // --- CHART SYNC LOGIC (Interaction) ---
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