import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { displayDate, displayRange, monthGrid, parseDate } from '../../core/dates';
import {
  CalendarDetail,
  CalendarEvent,
  CalendarEventType,
  CalendarInput,
  EVENT_TYPE_LABELS,
  EVENT_TYPES,
  EventInput,
  Permissions,
} from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { CalendarForm, EventForm } from '../../shared/forms';
import { Modal } from '../../shared/modal';

type EventDialog = { event: CalendarEvent | null; date: string | null };

@Component({
  selector: 'app-calendar-page',
  imports: [RouterLink, Modal, CalendarForm, EventForm],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.scss',
})
export class CalendarPage {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly calendarId = input.required<string>();

  protected readonly calendar = signal<CalendarDetail | null>(null);
  protected readonly perms = signal<Permissions>({ view: true, edit: false, publish: false, delete: false });
  protected readonly view = signal<'month' | 'list'>('month');
  protected readonly month = signal(new Date());
  protected readonly hiddenTypes = signal<ReadonlySet<CalendarEventType>>(new Set());
  protected readonly editingCalendar = signal(false);
  protected readonly eventDialog = signal<EventDialog | null>(null);

  protected readonly types = EVENT_TYPES;
  protected readonly labels = EVENT_TYPE_LABELS;
  protected readonly weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  protected readonly displayDate = displayDate;
  protected readonly displayRange = displayRange;

  protected readonly visibleEvents = computed(() => {
    const hidden = this.hiddenTypes();
    return (this.calendar()?.events ?? []).filter((e) => !hidden.has(e.type));
  });

  protected readonly typeCounts = computed(() => {
    const counts = new Map<CalendarEventType, number>();
    for (const e of this.calendar()?.events ?? []) counts.set(e.type, (counts.get(e.type) ?? 0) + 1);
    return counts;
  });

  protected readonly cells = computed(() => {
    const c = this.calendar();
    if (!c) return [];
    const events = this.visibleEvents();
    return monthGrid(this.month(), c.startDate, c.endDate).map((cell) => ({
      ...cell,
      events: events.filter((e) => e.startDate <= cell.date && e.endDate >= cell.date),
    }));
  });

  protected readonly monthLabel = computed(() =>
    this.month().toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
  );

  protected readonly canGoBack = computed(() => {
    const c = this.calendar();
    if (!c) return false;
    const start = parseDate(c.startDate);
    const m = this.month();
    return m.getFullYear() * 12 + m.getMonth() > start.getFullYear() * 12 + start.getMonth();
  });

  protected readonly canGoForward = computed(() => {
    const c = this.calendar();
    if (!c) return false;
    const end = parseDate(c.endDate);
    const m = this.month();
    return m.getFullYear() * 12 + m.getMonth() < end.getFullYear() * 12 + end.getMonth();
  });

  /** Events grouped by month for the list view. */
  protected readonly groupedEvents = computed(() => {
    const groups: { label: string; events: CalendarEvent[] }[] = [];
    for (const e of this.visibleEvents()) {
      const label = parseDate(e.startDate).toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
      const last = groups.at(-1);
      if (last?.label === label) last.events.push(e);
      else groups.push({ label, events: [e] });
    }
    return groups;
  });

  constructor() {
    effect(() => {
      const id = this.calendarId();
      untracked(() => this.load(id, true));
    });
  }

  private load(id: string, resetMonth = false) {
    forkJoin({ calendar: this.api.getCalendar(id), perms: this.api.getPermissions('calendar', id) }).subscribe({
      next: ({ calendar, perms }) => {
        this.calendar.set(calendar);
        this.perms.set(perms);
        if (resetMonth) this.month.set(this.initialMonth(calendar));
      },
      error: () => this.router.navigate(['/schools']),
    });
  }

  /** Today's month if it falls inside the calendar, otherwise the first month of the calendar. */
  private initialMonth(c: CalendarDetail) {
    const today = new Date();
    const start = parseDate(c.startDate);
    const end = parseDate(c.endDate);
    const inside = today >= start && today <= end;
    const d = inside ? today : start;
    return new Date(d.getFullYear(), d.getMonth(), 1);
  }

  shiftMonth(delta: number) {
    const m = this.month();
    this.month.set(new Date(m.getFullYear(), m.getMonth() + delta, 1));
  }

  toggleType(type: CalendarEventType) {
    this.hiddenTypes.update((set) => {
      const next = new Set(set);
      if (next.has(type)) next.delete(type); else next.add(type);
      return next;
    });
  }

  openNewEvent(date: string | null = null) {
    if (this.perms().edit) this.eventDialog.set({ event: null, date });
  }

  openEvent(event: CalendarEvent, domEvent?: Event) {
    domEvent?.stopPropagation();
    if (this.perms().edit) this.eventDialog.set({ event, date: null });
  }

  saveEvent(input: EventInput) {
    const dialog = this.eventDialog();
    const id = this.calendarId();
    const request: Observable<unknown> = dialog?.event
      ? this.api.updateEvent(id, dialog.event.id, input)
      : this.api.addEvent(id, input);
    request.subscribe(() => {
      this.toast.success(dialog?.event ? 'Event updated.' : 'Event added.');
      this.eventDialog.set(null);
      this.load(id);
    });
  }

  deleteEvent() {
    const event = this.eventDialog()?.event;
    if (!event || !confirm(`Delete "${event.title}"?`)) return;
    this.api.deleteEvent(this.calendarId(), event.id).subscribe(() => {
      this.toast.success('Event deleted.');
      this.eventDialog.set(null);
      this.load(this.calendarId());
    });
  }

  updateCalendar(input: CalendarInput) {
    this.api.updateCalendar(this.calendarId(), input).subscribe(() => {
      this.toast.success('Calendar updated.');
      this.editingCalendar.set(false);
      this.load(this.calendarId());
    });
  }

  togglePublished(c: CalendarDetail) {
    const publish = c.status !== 'Published';
    this.api.setPublished(c.id, publish).subscribe(() => {
      this.toast.success(publish ? 'Calendar published. Families and staff can now see it.' : 'Calendar moved back to draft.');
      this.load(c.id);
    });
  }

  remove(c: CalendarDetail) {
    if (!confirm(`Delete ${c.name}? This cannot be undone.`)) return;
    this.api.deleteCalendar(c.id).subscribe(() => {
      this.toast.success(`${c.name} deleted.`);
      this.router.navigate(['/programs', c.programId]);
    });
  }

  exportIcs(c: CalendarDetail) {
    this.api.exportIcs(c.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${c.name.replace(/[^\w-]+/g, '_')}.ics`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }
}
