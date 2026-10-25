import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, throwError } from 'rxjs';
import { ToastService } from './toast.service';
import { ProblemDetails } from './models';

interface Session {
  accessToken: string;
  userId: string;
  expiresAt: string;
}

const STORAGE_KEY = 'school-calendar.session';

/**
 * Holds the signed-in user's JWT. In development the API issues tokens for any user id
 * (/api/auth/dev-token) so the Permify roles can be explored; swap this for your IdP (OIDC) in production.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<Session | null>(this.restore());

  readonly userId = computed(() => this.session()?.userId ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);

  get token(): string | null {
    return this.session()?.accessToken ?? null;
  }

  signIn(userId: string) {
    return this.http.post<Session>('/api/auth/dev-token', { userId }).pipe(
      map((s) => {
        this.session.set(s);
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(s)); } catch { /* storage unavailable */ }
        return s;
      }),
    );
  }

  signOut() {
    this.session.set(null);
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* storage unavailable */ }
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const s = JSON.parse(raw) as Session;
      return new Date(s.expiresAt) > new Date() ? s : null;
    } catch {
      return null;
    }
  }
}

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isSignedIn() ? true : inject(Router).createUrlTree(['/login']);
};

/** Adds the bearer token and turns API problem responses into user-facing messages. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);

  const token = auth.token;
  const request = token && req.url.startsWith('/api/') ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      const problem = (error.error ?? {}) as ProblemDetails;
      switch (error.status) {
        case 401:
          auth.signOut();
          router.navigate(['/login']);
          break;
        case 403:
          toast.error('You do not have permission to do that.');
          break;
        case 400:
          // Validation errors are shown inline by forms; only surface a summary.
          toast.error(Object.values(problem.errors ?? {}).flat()[0] ?? problem.detail ?? 'Please check the form.');
          break;
        case 0:
        case 503:
          toast.error(problem.detail ?? 'The server is unavailable. Please try again.');
          break;
        default:
          toast.error(problem.detail ?? 'Something went wrong.');
      }
      return throwError(() => error);
    }),
  );
};
