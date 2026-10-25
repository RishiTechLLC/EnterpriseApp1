export interface School {
  id: string;
  name: string;
  code: string;
  address: string | null;
  timeZone: string;
  programCount: number;
  createdAt: string;
}

export interface Program {
  id: string;
  schoolId: string;
  name: string;
  code: string;
  description: string | null;
  gradeLevels: string | null;
  calendarCount: number;
}

export type CalendarStatus = 'Draft' | 'Published';

export interface CalendarSummary {
  id: string;
  programId: string;
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
  status: CalendarStatus;
  eventCount: number;
}

export interface CalendarDetail extends Omit<CalendarSummary, 'eventCount'> {
  schoolId: string;
  programName: string;
  schoolName: string;
  description: string | null;
  publishedAt: string | null;
  instructionalDays: number;
  events: CalendarEvent[];
}

export const EVENT_TYPES = [
  'TermStart',
  'TermEnd',
  'Holiday',
  'Break',
  'Exam',
  'ProfessionalDevelopment',
  'EarlyDismissal',
  'ParentTeacherConference',
  'SchoolEvent',
  'Other',
] as const;
export type CalendarEventType = (typeof EVENT_TYPES)[number];

export const EVENT_TYPE_LABELS: Record<CalendarEventType, string> = {
  TermStart: 'Term start',
  TermEnd: 'Term end',
  Holiday: 'Holiday',
  Break: 'Break',
  Exam: 'Exam',
  ProfessionalDevelopment: 'PD day',
  EarlyDismissal: 'Early dismissal',
  ParentTeacherConference: 'Conferences',
  SchoolEvent: 'School event',
  Other: 'Other',
};

export interface CalendarEvent {
  id: string;
  calendarId: string;
  title: string;
  description: string | null;
  type: CalendarEventType;
  startDate: string; // yyyy-MM-dd
  endDate: string;
  isInstructionalDay: boolean;
}

export type EventInput = Omit<CalendarEvent, 'id' | 'calendarId'>;

export interface SchoolInput {
  name: string;
  code: string;
  address: string | null;
  timeZone: string;
}

export interface ProgramInput {
  name: string;
  code: string;
  description: string | null;
  gradeLevels: string | null;
}

export interface CalendarInput {
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
  description: string | null;
}

export interface Member {
  userId: string;
  role: string;
}

export type ResourceType = 'school' | 'program' | 'calendar';

/** Permission flags returned by /api/access/{type}/{id}/permissions (names match the Permify schema). */
export interface Permissions {
  view: boolean;
  edit: boolean;
  manage?: boolean;
  publish?: boolean;
  delete?: boolean;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
