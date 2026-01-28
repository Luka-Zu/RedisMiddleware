import { Component, EventEmitter, Output } from '@angular/core';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent {
  @Output() openReplay = new EventEmitter<void>();

  constructor(public dashboard: DashboardService) {}

  onFilterChange() {
    this.dashboard.loadHistory();
  }
}