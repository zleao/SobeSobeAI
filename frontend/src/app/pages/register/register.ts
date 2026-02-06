import { Component, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../services/auth';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class Register {
  registerForm: FormGroup;
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  isLoading = signal(false);

  constructor(
    private fb: FormBuilder,
    private authService: Auth,
    private router: Router
  ) {
    this.registerForm = this.fb.group({
      username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(20), Validators.pattern(/^[a-zA-Z0-9_]+$/)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]]
    });
  }

  onSubmit(): void {
    if (this.registerForm.invalid) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.authService.register(this.registerForm.value).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.successMessage.set('Registration successful! Redirecting to login...');
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 2000);
      },
      error: (error) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.getErrorMessage(error));
      }
    });
  }

  private getErrorMessage(error: unknown): string {
    if (typeof error === 'string') {
      return error;
    }

    if (error && typeof error === 'object') {
      const maybeError = error as { error?: unknown; message?: unknown; statusText?: unknown };
      const payload = maybeError.error;
      if (typeof payload === 'string') {
        return payload;
      }

      if (payload && typeof payload === 'object') {
        const payloadObj = payload as { error?: unknown; message?: unknown; errors?: unknown };
        if (typeof payloadObj.error === 'string') {
          return payloadObj.error;
        }
        if (typeof payloadObj.message === 'string') {
          return payloadObj.message;
        }
        if (Array.isArray(payloadObj.errors) && payloadObj.errors.length > 0) {
          return String(payloadObj.errors[0]);
        }
      }

      if (typeof maybeError.message === 'string') {
        return maybeError.message;
      }

      if (typeof maybeError.statusText === 'string' && maybeError.statusText.length > 0) {
        return maybeError.statusText;
      }
    }

    return 'Registration failed. Please try again.';
  }
}
