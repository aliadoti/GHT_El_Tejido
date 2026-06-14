import { Routes } from '@angular/router';

import { adminGuard, authGuard, loginRedirectGuard } from './core/auth.guard';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [loginRedirectGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        title: 'Dashboard - El Tejido',
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'usuarios',
        title: 'Usuarios - El Tejido',
        loadComponent: () =>
          import('./features/usuarios/usuarios.page').then((m) => m.UsuariosPage),
      },
      {
        path: 'campanias',
        title: 'Campanias - El Tejido',
        loadComponent: () =>
          import('./features/campanias/campanias.page').then((m) => m.CampaniasPage),
      },
      {
        path: 'campanias/:id/envios',
        title: 'Envios - El Tejido',
        loadComponent: () => import('./features/envios/envios.page').then((m) => m.EnviosPage),
      },
      {
        path: 'envios',
        redirectTo: 'campanias/_/envios',
      },
      {
        path: 'rubricas',
        canActivate: [adminGuard],
        title: 'Rubricas - El Tejido',
        loadComponent: () =>
          import('./features/rubricas/rubricas.page').then((m) => m.RubricasPage),
      },
      {
        path: 'prompts',
        canActivate: [adminGuard],
        title: 'Prompts - El Tejido',
        loadComponent: () => import('./features/prompts/prompts.page').then((m) => m.PromptsPage),
      },
      {
        path: 'config-llm',
        canActivate: [adminGuard],
        title: 'Config LLM - El Tejido',
        loadComponent: () =>
          import('./features/config-llm/config-llm.page').then((m) => m.ConfigLlmPage),
      },
      {
        path: 'resultados',
        title: 'Resultados - El Tejido',
        loadComponent: () =>
          import('./features/resultados/resultados.page').then((m) => m.ResultadosPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
