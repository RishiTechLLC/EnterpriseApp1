import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { displayRange } from '../../core/dates';
import { CalendarInput, CalendarSummary, Permissions, Program, ProgramInput, School } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { CalendarForm, ProgramForm } from '../../shared/forms';
import { MembersPanel } from '../../shared/members-panel';
import { Modal } from '../../shared/modal';

@Component({
  selector: 'app-program-detail-page',
  imports: [RouterLink, Modal, ProgramForm, CalendarForm, MembersPanel],
  template: `
    <main class="container">
      @if (program(); as p) {
        <nav class="breadcrumbs">
          <a routerLink="/schools">Schools</a><span class="sep">/</span>
          <a [routerLink]="['/schools', p.schoolId]">{{ school()?.name ?? 'School' }}</a>
        </nav>
        <div class="page-header">
          <div>
            <h1>{{ p.name }}</h1>
            <p class="subtitle">
              {{ p.code }}@if (p.gradeLevels) { · Grades {{ p.gradeLevels }} }@if (p.description) { · {{ p.description }} }
            </p>
          </div>
          <div class="row">
            @if (perms().manage) {
              <button class="btn" (click)="editing.set(true)">Edit program</button>
              <button class="btn danger" (click)="remove(p)">Delete</button>
            }
            @if (perms().edit) { <button class="btn primary" (click)="addingCalendar.set(true)">+ New calendar</button> }
          </div>
        </div>

        <div class="layout">
          <section class="card">
            <h2>Calendars</h2>
            @if (calendars().length === 0) {
              <p class="empty">
                @if (perms().edit) { No calendars yet. Create one for the upcoming academic year. }
                @else { No published calendars yet. }
              </p>
            } @else {
              <ul class="list">
                @for (c of calendars(); track c.id) {
                  <li>
                    <div class="grow">
                      <a [routerLink]="['/calendars', c.id]"><strong>{{ c.name }}</strong></a>
                      <div class="muted small">{{ c.academicYear }} · {{ range(c.startDate, c.endDate) }} · {{ c.eventCount }} events</div>
                    </div>
                    <span class="badge" [class.published]="c.status === 'Published'" [class.draft]="c.status === 'Draft'">{{ c.status }}</span>
                  </li>
                }
              </ul>
            }
          </section>

          @if (perms().manage) {
            <aside>
              <app-members-panel resourceType="program" [resourceId]="p.id" [roles]="['coordinator']" heading="Program coordinators" />
            </aside>
          }
        </div>

        @if (editing()) {
          <app-modal title="Edit program" (closed)="editing.set(false)">
            <app-program-form [value]="p" (saved)="update($event)" (cancelled)="editing.set(false)" />
          </app-modal>
        }
        @if (addingCalendar()) {
          <app-modal title="New calendar" (closed)="addingCalendar.set(false)">
            <app-calendar-form (saved)="createCalendar($event)" (cancelled)="addingCalendar.set(false)" />
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
    .grow { flex: 1; }
  `,
})
export class ProgramDetailPage {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly programId = input.required<string>();

  protected readonly program = signal<Program | null>(null);
  protected readonly school = signal<School | null>(null);
  protected readonly calendars = signal<CalendarSummary[]>([]);
  protected readonly perms = signal<Permissions>({ view: true, edit: false, manage: false });
  protected readonly editing = signal(false);
  protected readonly addingCalendar = signal(false);
  protected readonly range = displayRange;

  constructor() {
    effect(() => {
      const id = this.programId();
      untracked(() => this.load(id));
    });
  }

  private load(id: string) {
    forkJoin({
      program: this.api.getProgram(id),
      calendars: this.api.getCalendars(id),
      perms: this.api.getPermissions('program', id),
    }).subscribe({
      next: ({ program, calendars, perms }) => {
        this.program.set(program);
        this.calendars.set(calendars);
        this.perms.set(perms);
        // The school name is only for the breadcrumb; coordinators may lack school access, so ignore failures.
        this.api.getSchool(program.schoolId).subscribe({ next: (s) => this.school.set(s), error: () => {} });
      },
      error: () => this.router.navigate(['/schools']),
    });
  }

  update(input: ProgramInput) {
    this.api.updateProgram(this.programId(), input).subscribe(() => {
      this.toast.success('Program updated.');
      this.editing.set(false);
      this.load(this.programId());
    });
  }

  createCalendar(input: CalendarInput) {
    this.api.createCalendar(this.programId(), input).subscribe((id) => {
      this.toast.success(`${input.name} created as a draft.`);
      this.router.navigate(['/calendars', id]);
    });
  }

  remove(p: Program) {
    if (!confirm(`Delete ${p.name} and all of its calendars? This cannot be undone.`)) return;
    this.api.deleteProgram(p.id).subscribe(() => {
      this.toast.success(`${p.name} deleted.`);
      this.router.navigate(['/schools', p.schoolId]);
    });
  }
}
