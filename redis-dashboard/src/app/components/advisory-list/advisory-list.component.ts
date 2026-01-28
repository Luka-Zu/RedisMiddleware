import { Component } from '@angular/core';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-advisory-list',
  templateUrl: './advisory-list.component.html',
  styleUrls: ['./advisory-list.component.scss']
})
export class AdvisoryListComponent {
  constructor(public dashboard: DashboardService) {}

  closeAdvisory(alert: any) {
    this.dashboard.removeAdvisory(alert);
  }
}