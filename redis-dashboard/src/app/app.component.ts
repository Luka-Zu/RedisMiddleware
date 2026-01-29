import { Component, OnInit } from '@angular/core';
import { DashboardService } from './services/dashboard.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  // Only Replay Modal logic remains here for now
  public isReplayModalOpen = false;
  public replayConfig = {
    from: new Date(Date.now() - 3600000).toISOString().slice(0, 16),
    to: new Date().toISOString().slice(0, 16),
    targetHost: 'localhost',
    targetPort: 6379,
    speed: 1.0
  };

  constructor(public dashboard: DashboardService) { }

  ngOnInit() {
    this.dashboard.init();
  }

  public openReplayModal() { this.isReplayModalOpen = true; }
  public closeReplayModal() { this.isReplayModalOpen = false; }

  public startReplay() {
    const payload = {
      ...this.replayConfig,
      from: new Date(this.replayConfig.from).toISOString(),
      to: new Date(this.replayConfig.to).toISOString()
    };

    this.dashboard.startReplay(payload).subscribe({
      next: () => {
        alert('Replay Started! Watch your Redis Monitor.');
        this.closeReplayModal();
      },
      error: (err: any) => alert('Failed to start replay: ' + err.message)
    });
  }

  public stopPropagation(event: Event) {
    event.stopPropagation();
  }
}