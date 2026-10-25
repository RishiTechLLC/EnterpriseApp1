import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Permissions, Program, ProgramInput, School, SchoolInput } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { ProgramForm, SchoolForm } from '../../shared/forms';
import { MembersPanel } from '../../shared/members-panel';
import { Modal } from '../../shared/modal';

@Component({
  selector: 'app-school-detail-page',
  imports: [RouterLink, Modal, SchoolForm, ProgramForm, MembersPanel],
  template: `
    <main class="container">
      @if (school(); as s) {
        <nav class="breadcrumbs"><a routerLink="/schools">Schools</a></nav>
        <div class="page-header">
          <div>
            <h1>{{ s.name }}</h1>
            <p class="subtitle">{{ s.code }} · {{ s.address ?? 'No address' }} · {{ s.timeZone }}</p>
          </div>
          <div class="row">
            @if (perms().edit) { <button class="btn" (click)="editing.set(true)">Edit school</button> }
            @if (perms().manage) {
              <button class="btn danger" (click)="remove(s)">Delete</button>
              <button class="btn primary" (click)="addingProgram.set(true)">+ New program</button>
            }
          </div>
        </div>

        <div class="layout">
          <section class="stack">
            <h2>Programs</h2>
            @if (programs().length === 0) {
              <div class="card empty">No programs yet. Each program (e.g. Elementary, High School, IB) gets its own calendars.</div>
            } @else {
              <div class="grid">
                @for (p of programs(); track p.id) {
                  <a class="card program" [routerLink]="['/programs', p.id]">
                    <div class="row">
                      <h3>{{ p.name }}</h3>
                      <span class="spacer"></span>
                      <span class="badge">{{ p.code }}</span>
                    </div>
                    @if (p.description) { <p class="muted small">{{ p.description }}</p> }
                    <div class="row muted small">
                      @if (p.gradeLevels) { <span>Grades {{ p.gradeLevels }}</span><span>·</span> }
                      <span>{{ p.calendarCount }} {{ p.calendarCount === 1 ? 'calendar' : 'calendars' }}</span>
                    </div>
                  </a>
                }
              </div>
            }
          </section>

          @if (perms().manage) {
            <aside>
              <app-members-panel resourceType="school" [resourceId]="s.id" [roles]="['admin', 'staff', 'viewer']" />
            </aside>
          }
        </div>

        @if (editing()) {
          <app-modal title="Edit school" (closed)="editing.set(false)">
            <app-school-form [value]="s" (saved)="update($event)" (cancelled)="editing.set(false)" />
          </app-modal>
        }
        @if (addingProgram()) {
          <app-modal title="New program" (closed)="addingProgram.set(false)">
            <app-program-form (saved)="createProgram($event)" (cancelled)="addingProgram.set(false)" />
          </app-modal>
        }
      } @else {
        <p class="loading">Loading…</p>
      }
    </main>
  `,
  styles: `
    .layout { display: grid; gap: 24px; grid-template-columns: 1fr; }
    @media (min-width: 960px) { .layout:has(aside) { grid-template-columns: 1fr 340px; } }
    .program { display: flex; flex-direction: column; gap: 8px; color: inherit;
      &:hover { border-color: var(--primary); text-decoration: none; } p { margin: 0; } }
  `,
})
export class SchoolDetailPage {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly schoolId = input.required<string>();

  protected readonly school = signal<School | null>(null);
  protected readonly programs = signal<Program[]>([]);
  protected readonly perms = signal<Permissions>({ view: true, edit: false, manage: false });
  protected readonly editing = signal(false);
  protected readonly addingProgram = signal(false);

  constructor() {
    effect(() => {
      const id = this.schoolId();
      untracked(() => this.load(id));
    });
  }

  private load(id: string) {
    forkJoin({
      school: this.api.getSchool(id),
      programs: this.api.getPrograms(id),
      perms: this.api.getPermissions('school', id),
    }).subscribe({
      next: ({ school, programs, perms }) => {
        this.school.set(school);
        this.programs.set(programs);
        this.perms.set(perms);
      },
      error: () => this.router.navigate(['/schools']),
    });
  }

  update(input: SchoolInput) {
    this.api.updateSchool(this.schoolId(), input).subscribe(() => {
      this.toast.success('School updated.');
      this.editing.set(false);
      this.load(this.schoolId());
    });
  }

  createProgram(input: ProgramInput) {
    this.api.createProgram(this.schoolId(), input).subscribe((id) => {
      this.toast.success(`${input.name} created.`);
      this.router.navigate(['/programs', id]);
    });
  }

  remove(s: School) {
    if (!confirm(`Delete ${s.name} and all of its programs and calendars? This cannot be undone.`)) return;
    this.api.deleteSchool(s.id).subscribe(() => {
      this.toast.success(`${s.name} deleted.`);
      this.router.navigate(['/schools']);
    });
  }
}
