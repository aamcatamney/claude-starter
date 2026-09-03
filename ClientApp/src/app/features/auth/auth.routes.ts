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
    path: 'forgot-password',
    title: 'Reset password · claude-starter',
    loadComponent: () => import('./forgot-password.page'),
  },
  {
    path: 'reset-password',
    title: 'Set a new password · claude-starter',
    loadComponent: () => import('./reset-password.page'),
  },
  {
    path: 'verify-email',
    title: 'Confirm your email · claude-starter',
    loadComponent: () => import('./verify-email.page'),
  },
  {
    path: 'register',
    title: 'Create account · claude-starter',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./register.page'),
  },
];
