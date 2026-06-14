import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { TagAdmin, UsuarioAdmin } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-usuarios-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 12-13</p>
          <h2>Usuarios y tags</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <section class="panel">
        <div class="panel-heading">
          <h3>Filtros</h3>
        </div>
        <form class="filters-grid" (ngSubmit)="load()">
          <label>
            Rol
            <select name="rol" [(ngModel)]="filtroRol">
              <option value="">Todos</option>
              <option value="participante">Participante</option>
              <option value="admin">Admin</option>
              <option value="visor">Visor</option>
            </select>
          </label>
          <label>
            Estado
            <select name="estado" [(ngModel)]="filtroEstado">
              <option value="">Todos</option>
              <option value="activo">Activo</option>
              <option value="inactivo">Inactivo</option>
            </select>
          </label>
          <label>
            Busqueda
            <input name="q" [(ngModel)]="filtroBusqueda" placeholder="Nombre o numero" />
          </label>
          <button class="primary-button" type="submit">Filtrar</button>
        </form>
      </section>

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading">
            <h3>Usuarios</h3>
            <span class="muted">{{ usuarios().length }} visibles</span>
          </div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Numero</th>
                  <th>Rol</th>
                  <th>Area</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (usuario of usuarios(); track usuario.id) {
                  <tr>
                    <td>{{ usuario.nombre }}</td>
                    <td>{{ usuario.whatsappNormalizado }}</td>
                    <td>{{ usuario.rol }}</td>
                    <td>{{ usuario.area }}</td>
                    <td>
                      <span class="status-badge">{{ usuario.estado }}</span>
                    </td>
                    <td>
                      @if (auth.isAdmin()) {
                        <button
                          type="button"
                          class="table-button"
                          (click)="iniciarEdicion(usuario)"
                        >
                          Editar
                        </button>
                        <button type="button" class="table-button" (click)="toggleUsuario(usuario)">
                          {{ usuario.estado === 'activo' ? 'Inactivar' : 'Activar' }}
                        </button>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="empty-cell">No hay usuarios para el filtro actual.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>{{ editandoId() ? 'Editar usuario' : 'Crear usuario' }}</h3>
            @if (editandoId()) {
              <button type="button" class="ghost-button" (click)="cancelarEdicion()">
                Cancelar
              </button>
            }
          </div>
          <form class="form-grid" (ngSubmit)="guardarUsuario()">
            <label>Nombre <input name="nombre" [(ngModel)]="nuevoUsuario.nombre" required /></label>
            <label>Numero <input name="numero" [(ngModel)]="nuevoUsuario.numero" required /></label>
            <label>
              Rol
              <select name="rolNuevo" [(ngModel)]="nuevoUsuario.rol">
                <option value="participante">Participante</option>
                <option value="admin">Admin</option>
                <option value="visor">Visor</option>
              </select>
            </label>
            <label>Area <input name="area" [(ngModel)]="nuevoUsuario.area" required /></label>
            <label
              >Empresa <input name="empresa" [(ngModel)]="nuevoUsuario.empresa" required
            /></label>
            <label
              >Tags <input name="tags" [(ngModel)]="tagsTexto" placeholder="t_area,t_empresa"
            /></label>
            <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
              {{ editandoId() ? 'Actualizar usuario' : 'Guardar usuario' }}
            </button>
          </form>
        </section>
      </div>

      <section class="panel">
        <div class="panel-heading">
          <h3>Tags</h3>
          <span class="muted">{{ tags().length }} visibles</span>
        </div>
        <form class="inline-form" (ngSubmit)="crearTag()">
          <input name="tagNombre" [(ngModel)]="nuevoTag.nombre" placeholder="Nombre" />
          <input name="tagTipo" [(ngModel)]="nuevoTag.tipoTag" placeholder="Tipo" />
          <input
            name="tagDescripcion"
            [(ngModel)]="nuevoTag.descripcion"
            placeholder="Descripcion"
          />
          <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
            Crear tag
          </button>
        </form>
        <div class="chip-row">
          @for (tag of tags(); track tag.id) {
            <span class="data-chip"
              >{{ tag.nombre }} <small>{{ tag.tipoTag }}</small></span
            >
          } @empty {
            <span class="muted">Sin tags registrados.</span>
          }
        </div>
      </section>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsuariosPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  protected readonly usuarios = signal<UsuarioAdmin[]>([]);
  protected readonly tags = signal<TagAdmin[]>([]);
  protected readonly error = signal('');
  protected readonly editandoId = signal<string | null>(null);

  protected filtroRol = '';
  protected filtroEstado = '';
  protected filtroBusqueda = '';
  protected tagsTexto = '';
  protected nuevoUsuario = {
    nombre: '',
    numero: '',
    rol: 'participante',
    area: '',
    empresa: '',
  };
  protected nuevoTag = {
    nombre: '',
    tipoTag: '',
    descripcion: '',
  };

  constructor() {
    this.load();
  }

  load() {
    this.api
      .usuarios({
        rol: this.filtroRol,
        estado: this.filtroEstado,
        q: this.filtroBusqueda,
        pageSize: 50,
      })
      .subscribe({
        next: (page) => {
          this.usuarios.set(page.items);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api.tags({ pageSize: 100 }).subscribe({
      next: (page) => this.tags.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  guardarUsuario() {
    const body = {
      ...this.nuevoUsuario,
      tags: this.tagsTexto
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean),
      propiedadesDinamicas: {},
    };
    const id = this.editandoId();
    const peticion = id ? this.api.actualizarUsuario(id, body) : this.api.crearUsuario(body);
    peticion.subscribe({
      next: () => {
        this.resetFormulario();
        this.load();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  iniciarEdicion(usuario: UsuarioAdmin) {
    this.editandoId.set(usuario.id);
    this.nuevoUsuario = {
      nombre: usuario.nombre,
      numero: usuario.whatsappNormalizado,
      rol: usuario.rol,
      area: usuario.area,
      empresa: usuario.empresa,
    };
    this.tagsTexto = (usuario.tags ?? []).join(',');
  }

  cancelarEdicion() {
    this.resetFormulario();
  }

  private resetFormulario() {
    this.editandoId.set(null);
    this.nuevoUsuario = {
      nombre: '',
      numero: '',
      rol: 'participante',
      area: '',
      empresa: '',
    };
    this.tagsTexto = '';
  }

  crearTag() {
    this.api.crearTag({ ...this.nuevoTag, estado: 'activo' }).subscribe({
      next: () => {
        this.nuevoTag = { nombre: '', tipoTag: '', descripcion: '' };
        this.load();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  toggleUsuario(usuario: UsuarioAdmin) {
    this.api
      .cambiarEstadoUsuario(usuario.id, usuario.estado === 'activo' ? 'inactivo' : 'activo')
      .subscribe({
        next: () => this.load(),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }
}
