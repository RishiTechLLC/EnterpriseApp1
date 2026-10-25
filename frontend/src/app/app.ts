import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth';
import { ToastService } from './core/toast.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header class="topbar">
      <a class="brand" routerLink="/schools">
        <span class="logo" aria-hidden="true">📅</span> School Calendars
      </a>
      @if (auth.userId(); as user) {
        <div class="user">
          <span class="avatar">{{ user[0].toUpperCase() }}</span>
          <span>{{ user }}</span>
          <button class="btn ghost sm" (click)="signOut()">Sign out</button>
        </div>
      }
    </header>

    <router-outlet />

    <div class="toasts" aria-live="polite">
      @for (t of toast.toasts(); track t.id) {
        <div class="toast" [class.error]="t.kind === 'error'" (click)="toast.dismiss(t.id)">{{ t.text }}</div>
      }
    </div>
  `,
  styles: `
    .topbar {
      display: flex; align-items: center; justify-content: space-between; gap: 16px;
      padding: 12px 24px; background: var(--surface); border-bottom: 1px solid var(--border);
      position: sticky; top: 0; z-index: 10;
    }
    .brand { font-weight: 700; font-size: 1.05rem; color: var(--text); display: flex; align-items: center; gap: 8px; }
    .brand:hover { text-decoration: none; }
    .user { display: flex; align-items: center; gap: 10px; font-size: 0.9rem; }
    .avatar {
      width: 28px; height: 28px; border-radius: 50%; background: var(--primary-soft); color: var(--primary);
      display: grid; place-items: center; font-weight: 700; font-size: 0.8rem;
    }
    .toasts { position: fixed; bottom: 20px; right: 20px; display: flex; flex-direction: column; gap: 8px; z-index: 100; }
    .toast {
      background: var(--success); color: #fff; padding: 10px 16px; border-radius: 8px; box-shadow: var(--shadow);
      cursor: pointer; max-width: 360px; font-size: 0.9rem;
    }
    .toast.error { background: var(--danger); }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  signOut() {
    this.auth.signOut();
    this.router.navigate(['/login']);
  }
}
