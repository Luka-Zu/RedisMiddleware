import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { Advisory } from '../interfaces/Advisory';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection: signalR.HubConnection;

  // RXJS Subjects to broadcast data to components
  public serverMetrics$ = new Subject<any>();
  public requestLogs$ = new Subject<any[]>();
  public advisories$ = new Subject<Advisory[]>();
  
  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/metrics', {
        // Important for CORS:
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();
  }

  public startConnection = () => {
    this.hubConnection
      .start()
      .then(() => console.log('SignalR Connection started'))
      .catch(err => console.log('Error while starting connection: ' + err));

    // Listen for "ReceiveServerUpdate" from .NET Backend
    this.hubConnection.on('ReceiveServerUpdate', (data) => {
      this.serverMetrics$.next(data);
    });

    // Listen for "ReceiveRequestLogUpdate" from .NET Backend
    this.hubConnection.on('ReceiveRequestLogUpdate', (data) => {
      this.requestLogs$.next(data);
    });

    this.hubConnection.on('ReceiveAdvisories', (data: Advisory[]) => {
      this.advisories$.next(data);
    });
  }
}