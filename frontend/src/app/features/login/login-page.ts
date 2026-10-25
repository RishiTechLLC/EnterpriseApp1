import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth';

interface DemoUser {
  id: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  template: `
    <main class="container login">
      <div class="intro">
        <h1>Sign in</h1>
        <p class="muted">
          Development sign-in. Pick a demo user to see how Permify grants different access to the same school calendars.
        </p>
      </div>

      <div class="grid">
        @for (u of demoUsers; track u.id) {
          <button class="card user-card" [disabled]="busy()" (click)="signIn(u.id)">
            <span class="name">{{ u.title }}</span>
            <code>{{ u.id }}</code>
            <span class="muted small">{{ u.description }}</span>
          </button>
        }
      </div>

      <form class="card custom" (ngSubmit)="signIn(customId)">
        <div class="field">
          <label for="uid">Or sign in as any user id</label>
          <input id="uid" name="uid" [(ngModel)]="customId" placeholder="e.g. jane.doe" autocomplete="off" />
        </div>
        <button class="btn primary" type="submit" [disabled]="!customId.trim() || busy()">Sign in</button>
      </form>
    </main>
  `,
  styles: `
    .login { max-width: 960px; }
    .intro { margin-bottom: 24px; p { max-width: 640px; } }
    .user-card {
      display: flex; flex-direction: column; gap: 6px; text-align: left; cursor: pointer; font: inherit; color: inherit;
      transition: border-color 0.15s, transform 0.15s;
      &:hover { border-color: var(--primary); transform: translateY(-1px); }
      .name { font-weight: 650; }
      code { font-size: 0.8rem; color: var(--primary); }
    }
    .custom { margin-top: 24px; display: flex; gap: 12px; align-items: flex-end; flex-wrap: wrap;
      .field { flex: 1; min-width: 220px; } }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected customId = '';

  protected readonly demoUsers: DemoUser[] = [
    { id: 'district-admin', title: 'District administrator', description: 'Organization admin: manages every school and can create new schools.' },
    { id: 'principal', title: 'Principal', description: 'Admin of Lincoln Unified: manages programs, calendars, publishing and staff roles.' },
    { id: 'coordinator', title: 'High School coordinator', description: 'Edits High School calendars at Lincoln, but cannot publish them.' },
    { id: 'teacher', title: 'Teacher', description: 'Lincoln staff: views published calendars for every program.' },
    { id: 'parent', title: 'Parent', description: 'Lincoln viewer: sees published calendars only.' },
    { id: 'riverside-admin', title: 'Riverside principal', description: 'Admin of Riverside Academy only: cannot see Lincoln.' },
  ];

  signIn(userId: string) {
    const id = userId.trim();
    if (!id) return;
    this.busy.set(true);
    this.auth.signIn(id).subscribe({
      next: () => this.router.navigate(['/schools']),
      error: () => this.busy.set(false),
    });
  }
}
