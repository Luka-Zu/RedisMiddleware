import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, tap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AuthService {
private baseUrl = 'http://localhost:5000';
  private tokenKey = 'jwt_token';
  // Check if token exists in localStorage
  isAuthenticated = new BehaviorSubject<boolean>(!!localStorage.getItem(this.tokenKey));

  constructor(private http: HttpClient) {}

  login(username: string, password: string) {
    return this.http.post<{ token: string }>(`${this.baseUrl}/api/auth/login`, { username, password })
      .pipe(tap(res => {
        localStorage.setItem(this.tokenKey, res.token);
        this.isAuthenticated.next(true);
      }));
  }

  getToken() {
    return localStorage.getItem(this.tokenKey);
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    this.isAuthenticated.next(false);
  }
}