/** Helpers for the API's date-only strings (yyyy-MM-dd), avoiding time-zone shifts from `new Date(string)`. */

export function parseDate(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function formatDate(date: Date): string {
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${m}-${d}`;
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

export function displayDate(value: string): string {
  return parseDate(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

export function displayRange(start: string, end: string): string {
  if (start === end) return displayDate(start);
  const s = parseDate(start);
  const e = parseDate(end);
  const sameYear = s.getFullYear() === e.getFullYear();
  const startText = s.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: sameYear ? undefined : 'numeric' });
  return `${startText} – ${displayDate(end)}`;
}

export interface MonthCell {
  date: string;
  day: number;
  inMonth: boolean;
  inRange: boolean;
  isWeekend: boolean;
  isToday: boolean;
}

/** 6x7 grid (Sunday first) for the month containing `month`. */
export function monthGrid(month: Date, rangeStart: string, rangeEnd: string): MonthCell[] {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const start = addDays(first, -first.getDay());
  const today = formatDate(new Date());
  return Array.from({ length: 42 }, (_, i) => {
    const date = addDays(start, i);
    const iso = formatDate(date);
    return {
      date: iso,
      day: date.getDate(),
      inMonth: date.getMonth() === month.getMonth(),
      inRange: iso >= rangeStart && iso <= rangeEnd,
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
      isToday: iso === today,
    };
  });
}
