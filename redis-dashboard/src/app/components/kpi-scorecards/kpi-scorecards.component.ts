import { Component } from '@angular/core';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-kpi-scorecards',
  templateUrl: './kpi-scorecards.component.html',
  styleUrls: ['./kpi-scorecards.component.scss']
})
export class KpiScorecardsComponent {
  constructor(public dashboard: DashboardService) {}
}