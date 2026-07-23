import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../core/auth.service';
import { BrandSignatureComponent } from './brand-signature.component';
import { NotificacionesComponent } from './notificaciones.component';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  adminOnly?: boolean;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    BrandSignatureComponent,
    NotificacionesComponent,
  ],
  template: `
    <div class="portal-shell">
      <aside class="sidebar" aria-label="Navegacion principal">
        <a class="brand" routerLink="/">
          <img class="brand-mark" src="brand/eltejido-logo.png" alt="GHT" width="44" height="44" />
          <span>
            <strong>Tejido de Red</strong>
            <small>Banco de ideas</small>
          </span>
        </a>

        <nav class="nav-list">
          @for (item of navItems; track item.route) {
            @if (!item.adminOnly || auth.isAdmin()) {
              <a
                class="nav-link"
                [routerLink]="item.route"
                routerLinkActive="active"
                [routerLinkActiveOptions]="{ exact: item.route === '/' }"
              >
                <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span>
                <span>{{ item.label }}</span>
              </a>
            }
          }
        </nav>
      </aside>

      <main class="content-shell">
        <header class="topbar">
          <div>
            <p class="eyebrow">Portal administrativo</p>
            <h1>Gestion de campanias y resultados</h1>
          </div>
          <div class="session-box">
            <span>{{ auth.usuario()?.nombre ?? 'Sesion' }}</span>
            <strong>{{ auth.usuario()?.rol ?? '' }}</strong>
            <button type="button" class="ghost-button" (click)="logout()">Salir</button>
          </div>
        </header>

        <router-outlet />

        <app-notificaciones />

        <app-brand-signature />
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/', icon: 'D' },
    { label: 'Usuarios', route: '/usuarios', icon: 'U' },
    { label: 'Campanias', route: '/campanias', icon: 'C' },
    { label: 'Envios', route: '/campanias/_/envios', icon: 'E' },
    { label: 'Rubricas', route: '/rubricas', icon: 'R' },
    { label: 'Prompts', route: '/prompts', icon: 'P' },
    { label: 'Config LLM', route: '/config-llm', icon: 'L', adminOnly: true },
    { label: 'Resultados', route: '/resultados', icon: 'M' },
    { label: 'Simulacion WA', route: '/simulacion-whatsapp', icon: 'W', adminOnly: true },
    { label: 'Mantenimiento', route: '/mantenimiento', icon: 'X', adminOnly: true },
  ];

  logout() {
    this.auth.logout().subscribe();
  }
}
