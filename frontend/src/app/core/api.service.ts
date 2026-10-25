import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  CalendarDetail,
  CalendarEvent,
  CalendarEventType,
  CalendarInput,
  CalendarSummary,
  EventInput,
  Member,
  Permissions,
  Program,
  ProgramInput,
  ResourceType,
  School,
  SchoolInput,
} from './models';

/** Typed client for the School Calendar API. Every call is authorized server-side by Permify. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // Schools
  getSchools() { return this.http.get<School[]>(`${this.base}/schools`); }
  getSchool(id: string) { return this.http.get<School>(`${this.base}/schools/${id}`); }
  createSchool(input: SchoolInput) { return this.http.post<string>(`${this.base}/schools`, input); }
  updateSchool(id: string, input: SchoolInput) { return this.http.put<void>(`${this.base}/schools/${id}`, input); }
  deleteSchool(id: string) { return this.http.delete<void>(`${this.base}/schools/${id}`); }

  // Programs
  getPrograms(schoolId: string) { return this.http.get<Program[]>(`${this.base}/schools/${schoolId}/programs`); }
  getProgram(id: string) { return this.http.get<Program>(`${this.base}/programs/${id}`); }
  createProgram(schoolId: string, input: ProgramInput) {
    return this.http.post<string>(`${this.base}/schools/${schoolId}/programs`, input);
  }
  updateProgram(id: string, input: ProgramInput) { return this.http.put<void>(`${this.base}/programs/${id}`, input); }
  deleteProgram(id: string) { return this.http.delete<void>(`${this.base}/programs/${id}`); }

  // Calendars
  getCalendars(programId: string) { return this.http.get<CalendarSummary[]>(`${this.base}/programs/${programId}/calendars`); }
  getCalendar(id: string) { return this.http.get<CalendarDetail>(`${this.base}/calendars/${id}`); }
  createCalendar(programId: string, input: CalendarInput) {
    return this.http.post<string>(`${this.base}/programs/${programId}/calendars`, input);
  }
  updateCalendar(id: string, input: CalendarInput) { return this.http.put<void>(`${this.base}/calendars/${id}`, input); }
  deleteCalendar(id: string) { return this.http.delete<void>(`${this.base}/calendars/${id}`); }
  setPublished(id: string, published: boolean) {
    return this.http.post<void>(`${this.base}/calendars/${id}/${published ? 'publish' : 'unpublish'}`, {});
  }
  exportIcs(id: string) { return this.http.get(`${this.base}/calendars/${id}/export.ics`, { responseType: 'blob' }); }

  // Events
  getEvents(calendarId: string, filter: { from?: string; to?: string; type?: CalendarEventType } = {}) {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(filter)) if (v) params = params.set(k, v);
    return this.http.get<CalendarEvent[]>(`${this.base}/calendars/${calendarId}/events`, { params });
  }
  addEvent(calendarId: string, input: EventInput) {
    return this.http.post<CalendarEvent>(`${this.base}/calendars/${calendarId}/events`, input);
  }
  updateEvent(calendarId: string, eventId: string, input: EventInput) {
    return this.http.put<void>(`${this.base}/calendars/${calendarId}/events/${eventId}`, input);
  }
  deleteEvent(calendarId: string, eventId: string) {
    return this.http.delete<void>(`${this.base}/calendars/${calendarId}/events/${eventId}`);
  }

  // Access (Permify)
  canCreateSchool() { return this.http.get<boolean>(`${this.base}/access/can-create-school`); }
  getPermissions(type: ResourceType, id: string) {
    return this.http.get<Permissions>(`${this.base}/access/${type}/${id}/permissions`);
  }
  getMembers(type: ResourceType, id: string) { return this.http.get<Member[]>(`${this.base}/access/${type}/${id}/members`); }
  assignRole(type: ResourceType, id: string, userId: string, role: string) {
    return this.http.post<void>(`${this.base}/access/${type}/${id}/members`, { userId, role });
  }
  removeRole(type: ResourceType, id: string, userId: string, role: string) {
    return this.http.delete<void>(
      `${this.base}/access/${type}/${id}/members/${encodeURIComponent(role)}/${encodeURIComponent(userId)}`,
    );
  }
}
