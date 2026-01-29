import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-replay-modal',
  templateUrl: './replay-modal.component.html',
  styleUrls: ['./replay-modal.component.scss']
})
export class ReplayModalComponent {
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();

  public replayConfig = {
    from: new Date(Date.now() - 3600000).toISOString().slice(0, 16),
    to: new Date().toISOString().slice(0, 16),
    targetHost: 'localhost',
    targetPort: 6379,
    speed: 1.0
  };

  constructor(private dashboard: DashboardService) {}

  onClose() {
    this.close.emit();
  }

  startReplay() {
    const payload = {
      ...this.replayConfig,
      from: new Date(this.replayConfig.from).toISOString(),
      to: new Date(this.replayConfig.to).toISOString()
    };

    this.dashboard.startReplay(payload).subscribe({
      next: () => {
        alert('Replay Started! Watch your Redis Monitor.');
        this.onClose();
      },
      error: (err: any) => alert('Failed to start replay: ' + err.message)
    });
  }

  stopPropagation(event: Event) {
    event.stopPropagation();
  }
}