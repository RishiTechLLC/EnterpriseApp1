import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { displayRange, formatDate, monthGrid, parseDate } from './core/dates';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('renders the brand', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('.brand')?.textContent).toContain('School Calendars');
  });
});

describe('dates', () => {
  it('round-trips date-only strings without time-zone drift', () => {
    expect(formatDate(parseDate('2026-12-31'))).toBe('2026-12-31');
  });

  it('builds a Sunday-first 6-week month grid flagged against the calendar range', () => {
    const cells = monthGrid(new Date(2026, 8, 1), '2026-09-08', '2027-06-04'); // September 2026
    expect(cells.length).toBe(42);
    expect(cells[0].date).toBe('2026-08-30');
    expect(cells.find((c) => c.date === '2026-09-07')?.inRange).toBe(false);
    expect(cells.find((c) => c.date === '2026-09-08')?.inRange).toBe(true);
  });

  it('formats single days and ranges', () => {
    expect(displayRange('2026-11-25', '2026-11-25')).not.toContain('–');
    expect(displayRange('2026-12-21', '2027-01-01')).toContain('–');
  });
});
