import { Routes } from '@angular/router';
import { redirectIfAuthedGuard } from '../../core/auth/auth.guard';

export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    title: 'Sign in · claude-starter',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./login.page'),
  },
  {
    path: 'register',
    title: 'Create account · claude-starter',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./register.page'),
  },
];
