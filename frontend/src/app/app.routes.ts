import { Routes } from '@angular/router';
import { authGuard } from './core/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'schools' },
      { path: 'schools', loadComponent: () => import('./features/schools/school-list-page').then((m) => m.SchoolListPage) },
      {
        path: 'schools/:schoolId',
        loadComponent: () => import('./features/schools/school-detail-page').then((m) => m.SchoolDetailPage),
      },
      {
        path: 'programs/:programId',
        loadComponent: () => import('./features/programs/program-detail-page').then((m) => m.ProgramDetailPage),
      },
      {
        path: 'calendars/:calendarId',
        loadComponent: () => import('./features/calendars/calendar-page').then((m) => m.CalendarPage),
      },
    ],
  },
  { path: '**', redirectTo: 'schools' },
];
