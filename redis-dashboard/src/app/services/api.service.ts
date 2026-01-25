import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { KeyNode } from '../interfaces/KeyNode';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private baseUrl = 'http://localhost:5000/api/metrics';

  constructor(private http: HttpClient) { }

  getServerHistory(fromTime?: string): Observable<any[]> {
    let params = new HttpParams();
    if (fromTime) {
      params = params.set('from', fromTime);
    }
    return this.http.get<any[]>(`${this.baseUrl}/server/history`, { params });
  }

  getRequestHistory(fromTime?: string): Observable<any[]> {
    let params = new HttpParams();
    if (fromTime) {
      params = params.set('from', fromTime);
    }
    return this.http.get<any[]>(`${this.baseUrl}/requests/history`, { params });
  }

  getCommandStats(fromTime?: string): Observable<any[]> {
    let params = new HttpParams();
    if (fromTime) {
      params = params.set('from', fromTime);
    }
    return this.http.get<any[]>(`${this.baseUrl}/commands/stats`, { params });
  }

  getHotKeys(fromTime?: string): Observable<any[]> {
    let params = new HttpParams();
    if (fromTime) {
      params = params.set('from', fromTime);
    }
    return this.http.get<any[]>(`${this.baseUrl}/keys/hot`, { params });
  }

  public getKeyspace(isoDate: string): Observable<KeyNode> {
    return this.http.get<KeyNode>(`${this.baseUrl}/keyspace?from=${isoDate}`);
  }

  public startReplay(config: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/../replay/start`, config); // Note: Adjust URL path
  }
  
}