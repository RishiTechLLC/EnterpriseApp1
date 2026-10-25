import { Component, OnInit, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CalendarEvent,
  CalendarInput,
  EVENT_TYPE_LABELS,
  EVENT_TYPES,
  EventInput,
  ProgramInput,
  SchoolInput,
} from '../core/models';

const blankToNull = (v: string | null | undefined) => (v && v.trim() ? v.trim() : null);

@Component({
  selector: 'app-school-form',
  imports: [ReactiveFormsModule],
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="submit()">
      <div class="field">
        <label for="name">School name</label>
        <input id="name" formControlName="name" />
      </div>
      <div class="form-row">
        <div class="field">
          <label for="code">Code</label>
          <input id="code" formControlName="code" placeholder="e.g. LUK12" />
        </div>
        <div class="field">
          <label for="tz">Time zone</label>
          <select id="tz" formControlName="timeZone">
            @for (tz of timeZones; track tz) { <option [value]="tz">{{ tz }}</option> }
          </select>
        </div>
      </div>
      <div class="field">
        <label for="addr">Address</label>
        <input id="addr" formControlName="address" />
      </div>
      <div class="row">
        <span class="spacer"></span>
        <button class="btn" type="button" (click)="cancelled.emit()">Cancel</button>
        <button class="btn primary" type="submit" [disabled]="form.invalid">Save</button>
      </div>
    </form>
  `,
})
export class SchoolForm implements OnInit {
  readonly value = input<SchoolInput | null>(null);
  readonly saved = output<SchoolInput>();
  readonly cancelled = output<void>();

  protected readonly timeZones = [
    'America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles', 'America/Anchorage',
    'Pacific/Honolulu', 'Europe/London', 'Europe/Berlin', 'Asia/Kolkata', 'Asia/Singapore', 'Australia/Sydney', 'UTC',
  ];

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: ['', [Validators.required, Validators.maxLength(20)]],
    timeZone: ['America/Chicago', Validators.required],
    address: [''],
  });

  ngOnInit() {
    const v = this.value();
    if (v) this.form.patchValue({ ...v, address: v.address ?? '' });
  }

  submit() {
    const v = this.form.getRawValue();
    this.saved.emit({ ...v, name: v.name.trim(), code: v.code.trim(), address: blankToNull(v.address) });
  }
}

@Component({
  selector: 'app-program-form',
  imports: [ReactiveFormsModule],
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="submit()">
      <div class="field">
        <label for="pname">Program name</label>
        <input id="pname" formControlName="name" placeholder="e.g. Middle School, IB Diploma, Adult Education" />
      </div>
      <div class="form-row">
        <div class="field">
          <label for="pcode">Code</label>
          <input id="pcode" formControlName="code" placeholder="e.g. MS" />
        </div>
        <div class="field">
          <label for="grades">Grade levels</label>
          <input id="grades" formControlName="gradeLevels" placeholder="e.g. 6-8" />
        </div>
      </div>
      <div class="field">
        <label for="pdesc">Description</label>
        <textarea id="pdesc" formControlName="description"></textarea>
      </div>
      <div class="row">
        <span class="spacer"></span>
        <button class="btn" type="button" (click)="cancelled.emit()">Cancel</button>
        <button class="btn primary" type="submit" [disabled]="form.invalid">Save</button>
      </div>
    </form>
  `,
})
export class ProgramForm implements OnInit {
  readonly value = input<ProgramInput | null>(null);
  readonly saved = output<ProgramInput>();
  readonly cancelled = output<void>();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: ['', [Validators.required, Validators.maxLength(20)]],
    gradeLevels: [''],
    description: [''],
  });

  ngOnInit() {
    const v = this.value();
    if (v) this.form.patchValue({ ...v, gradeLevels: v.gradeLevels ?? '', description: v.description ?? '' });
  }

  submit() {
    const v = this.form.getRawValue();
    this.saved.emit({
      name: v.name.trim(),
      code: v.code.trim(),
      gradeLevels: blankToNull(v.gradeLevels),
      description: blankToNull(v.description),
    });
  }
}

@Component({
  selector: 'app-calendar-form',
  imports: [ReactiveFormsModule],
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="submit()">
      <div class="field">
        <label for="cname">Calendar name</label>
        <input id="cname" formControlName="name" placeholder="e.g. High School 2026-2027" />
      </div>
      <div class="form-row">
        <div class="field">
          <label for="year">Academic year</label>
          <input id="year" formControlName="academicYear" placeholder="2026-2027" />
          @if (form.controls.academicYear.touched && form.controls.academicYear.invalid) {
            <span class="error">Use the format 2026-2027.</span>
          }
        </div>
        <div class="field">
          <label for="start">First day</label>
          <input id="start" type="date" formControlName="startDate" />
        </div>
        <div class="field">
          <label for="end">Last day</label>
          <input id="end" type="date" formControlName="endDate" />
        </div>
      </div>
      @if (rangeInvalid()) { <span class="error small">The last day must be on or after the first day.</span> }
      <div class="field">
        <label for="cdesc">Description</label>
        <textarea id="cdesc" formControlName="description"></textarea>
      </div>
      <div class="row">
        <span class="spacer"></span>
        <button class="btn" type="button" (click)="cancelled.emit()">Cancel</button>
        <button class="btn primary" type="submit" [disabled]="form.invalid || rangeInvalid()">Save</button>
      </div>
    </form>
  `,
})
export class CalendarForm implements OnInit {
  readonly value = input<CalendarInput | null>(null);
  readonly saved = output<CalendarInput>();
  readonly cancelled = output<void>();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    academicYear: ['', [Validators.required, Validators.pattern(/^\d{4}-\d{4}$/)]],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
    description: [''],
  });

  ngOnInit() {
    const v = this.value();
    if (v) {
      this.form.patchValue({ ...v, description: v.description ?? '' });
    } else {
      const y = new Date().getMonth() >= 6 ? new Date().getFullYear() : new Date().getFullYear() - 1;
      this.form.patchValue({ academicYear: `${y}-${y + 1}`, startDate: `${y}-08-15`, endDate: `${y + 1}-06-05` });
    }
  }

  rangeInvalid() {
    const { startDate, endDate } = this.form.getRawValue();
    return !!startDate && !!endDate && endDate < startDate;
  }

  submit() {
    const v = this.form.getRawValue();
    this.saved.emit({ ...v, name: v.name.trim(), description: blankToNull(v.description) });
  }
}

@Component({
  selector: 'app-event-form',
  imports: [ReactiveFormsModule],
  template: `
    <form class="form" [formGroup]="form" (ngSubmit)="submit()">
      <div class="field">
        <label for="title">Title</label>
        <input id="title" formControlName="title" placeholder="e.g. Winter Break" />
      </div>
      <div class="form-row">
        <div class="field">
          <label for="type">Type</label>
          <select id="type" formControlName="type" (change)="onTypeChange()">
            @for (t of types; track t) { <option [value]="t">{{ labels[t] }}</option> }
          </select>
        </div>
        <div class="field">
          <label for="estart">Start</label>
          <input id="estart" type="date" formControlName="startDate" [min]="min()" [max]="max()" />
        </div>
        <div class="field">
          <label for="eend">End</label>
          <input id="eend" type="date" formControlName="endDate" [min]="form.controls.startDate.value || min()" [max]="max()" />
        </div>
      </div>
      <label class="checkbox">
        <input type="checkbox" formControlName="isInstructionalDay" />
        Students attend school (counts as an instructional day)
      </label>
      <div class="field">
        <label for="edesc">Description</label>
        <textarea id="edesc" formControlName="description"></textarea>
      </div>
      <div class="row">
        @if (event()) { <button class="btn danger" type="button" (click)="deleted.emit()">Delete</button> }
        <span class="spacer"></span>
        <button class="btn" type="button" (click)="cancelled.emit()">Cancel</button>
        <button class="btn primary" type="submit" [disabled]="form.invalid">Save</button>
      </div>
    </form>
  `,
})
export class EventForm implements OnInit {
  readonly event = input<CalendarEvent | null>(null);
  readonly defaultDate = input<string | null>(null);
  readonly min = input.required<string>();
  readonly max = input.required<string>();
  readonly saved = output<EventInput>();
  readonly deleted = output<void>();
  readonly cancelled = output<void>();

  protected readonly types = EVENT_TYPES;
  protected readonly labels = EVENT_TYPE_LABELS;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    type: ['Holiday' as EventInput['type'], Validators.required],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
    isInstructionalDay: [false],
    description: [''],
  });

  ngOnInit() {
    const e = this.event();
    if (e) {
      this.form.patchValue({ ...e, description: e.description ?? '' });
    } else {
      const d = this.defaultDate() ?? this.min();
      this.form.patchValue({ startDate: d, endDate: d });
    }
  }

  /** Holidays, breaks and PD days are usually days off; most other types happen on school days. */
  onTypeChange() {
    const offTypes = ['Holiday', 'Break', 'ProfessionalDevelopment'];
    this.form.controls.isInstructionalDay.setValue(!offTypes.includes(this.form.controls.type.value));
  }

  submit() {
    const v = this.form.getRawValue();
    if (v.endDate < v.startDate) v.endDate = v.startDate;
    this.saved.emit({ ...v, title: v.title.trim(), description: blankToNull(v.description) });
  }
}
