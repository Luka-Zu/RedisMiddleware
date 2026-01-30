import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from 'src/app/services/auth.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  username = '';
  password = '';
  errorMessage = '';
  isLoading = false;

  constructor(private authService: AuthService, private router: Router) {}

  onSubmit(): void {
    // Basic Validation
    if (!this.username || !this.password) {
      this.errorMessage = 'Please enter both username and password.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.username, this.password).subscribe({
      next: () => {
        // On success, redirect to the main dashboard
        this.isLoading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        // On failure, show error message
        this.isLoading = false;
        this.errorMessage = 'Invalid credentials. Please try again.';
        console.error('Login failed', err);
      }
    });
  }
}