import { Component } from '@angular/core';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-traffic-inspection',
  templateUrl: './traffic-inspection.component.html',
  styleUrls: ['./traffic-inspection.component.scss']
})
export class TrafficInspectionComponent {
  constructor(public dashboard: DashboardService) {}
}