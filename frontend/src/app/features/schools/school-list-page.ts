import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { School, SchoolInput } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { SchoolForm } from '../../shared/forms';
import { Modal } from '../../shared/modal';

@Component({
  selector: 'app-school-list-page',
  imports: [RouterLink, Modal, SchoolForm],
  template: `
    <main class="container">
      <div class="page-header">
        <div>
          <h1>Schools</h1>
          <p class="subtitle">Schools you have access to. Each school runs its own calendars per program.</p>
        </div>
        @if (canCreate()) {
          <button class="btn primary" (click)="creating.set(true)">+ New school</button>
        }
      </div>

      @if (loading()) {
        <p class="loading">Loading schools…</p>
      } @else if (schools().length === 0) {
        <div class="card empty">
          <h2>No schools yet</h2>
          <p>You haven't been given access to any school. Ask a school administrator to add you.</p>
        </div>
      } @else {
        <div class="grid">
          @for (s of schools(); track s.id) {
            <a class="card school" [routerLink]="['/schools', s.id]">
              <div class="row">
                <span class="initials">{{ s.code.slice(0, 3) }}</span>
                <div>
                  <h3>{{ s.name }}</h3>
                  <span class="muted small">{{ s.address ?? s.timeZone }}</span>
                </div>
              </div>
              <span class="muted small">{{ s.programCount }} {{ s.programCount === 1 ? 'program' : 'programs' }}</span>
            </a>
          }
        </div>
      }
    </main>

    @if (creating()) {
      <app-modal title="New school" (closed)="creating.set(false)">
        <app-school-form (saved)="create($event)" (cancelled)="creating.set(false)" />
      </app-modal>
    }
  `,
  styles: `
    .school { display: flex; flex-direction: column; gap: 14px; color: inherit; transition: border-color 0.15s;
      &:hover { border-color: var(--primary); text-decoration: none; } }
    .initials {
      width: 44px; height: 44px; border-radius: 10px; background: var(--primary-soft); color: var(--primary);
      display: grid; place-items: center; font-weight: 700; font-size: 0.8rem; flex: none;
    }
  `,
})
export class SchoolListPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly schools = signal<School[]>([]);
  protected readonly loading = signal(true);
  protected readonly canCreate = signal(false);
  protected readonly creating = signal(false);

  ngOnInit() {
    this.api.getSchools().subscribe({
      next: (s) => { this.schools.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.canCreateSchool().subscribe((can) => this.canCreate.set(can));
  }

  create(input: SchoolInput) {
    this.api.createSchool(input).subscribe((id) => {
      this.toast.success(`${input.name} created.`);
      this.router.navigate(['/schools', id]);
    });
  }
}
