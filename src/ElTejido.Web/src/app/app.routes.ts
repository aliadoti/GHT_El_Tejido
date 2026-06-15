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
    path: 'simulacion-whatsapp',
    title: 'Simulacion WhatsApp - Tejido de Red',
    loadComponent: () =>
      import('./features/simulacion-whatsapp/simulacion-whatsapp.page').then(
        (m) => m.SimulacionWhatsappPage,
      ),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        title: 'Dashboard - Tejido de Red',
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'usuarios',
        title: 'Usuarios - Tejido de Red',
        loadComponent: () =>
          import('./features/usuarios/usuarios.page').then((m) => m.UsuariosPage),
      },
      {
        path: 'campanias',
        title: 'Campanias - Tejido de Red',
        loadComponent: () =>
          import('./features/campanias/campanias.page').then((m) => m.CampaniasPage),
      },
      {
        path: 'campanias/:id/envios',
        title: 'Envios - Tejido de Red',
        loadComponent: () => import('./features/envios/envios.page').then((m) => m.EnviosPage),
      },
      {
        path: 'envios',
        redirectTo: 'campanias/_/envios',
      },
      {
        path: 'rubricas',
        canActivate: [adminGuard],
        title: 'Rubricas - Tejido de Red',
        loadComponent: () =>
          import('./features/rubricas/rubricas.page').then((m) => m.RubricasPage),
      },
      {
        path: 'prompts',
        canActivate: [adminGuard],
        title: 'Prompts - Tejido de Red',
        loadComponent: () => import('./features/prompts/prompts.page').then((m) => m.PromptsPage),
      },
      {
        path: 'config-llm',
        canActivate: [adminGuard],
        title: 'Config LLM - Tejido de Red',
        loadComponent: () =>
          import('./features/config-llm/config-llm.page').then((m) => m.ConfigLlmPage),
      },
      {
        path: 'resultados',
        title: 'Resultados - Tejido de Red',
        loadComponent: () =>
          import('./features/resultados/resultados.page').then((m) => m.ResultadosPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
