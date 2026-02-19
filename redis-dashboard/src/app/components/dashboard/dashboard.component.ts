import { Component, OnInit } from '@angular/core';
import { DashboardService } from 'src/app/services/dashboard.service';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit{

  public isReplayModalOpen = false;

  constructor(private dashboard: DashboardService) {}

  ngOnInit() {
    //this.dashboard.init();
  }
  
}
