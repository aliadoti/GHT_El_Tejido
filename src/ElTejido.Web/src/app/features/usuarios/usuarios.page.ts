import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { Campania, ReporteCargaMasiva, TagAdmin, UsuarioAdmin } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { NotificacionesService } from '../../core/notificaciones.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';
import {
  CargaMasivaPanel,
  FichaUsuarioPanel,
  FormularioReasignacion,
  SolicitudCargaMasiva,
} from './usuarios.panels';

@Component({
  selector: 'app-usuarios-page',
  standalone: true,
  imports: [FormsModule, EstadoAccesibleComponent, CargaMasivaPanel, FichaUsuarioPanel],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <h2>Usuarios y tags</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      <app-estado-accesible tipo="error" [mensaje]="error()" />

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
            ID empresa
            <input name="empresaId" [(ngModel)]="filtroEmpresaId" placeholder="AL, GR, FF…" />
          </label>
          <label>
            Sede
            <input name="sede" [(ngModel)]="filtroSede" />
          </label>
          <label>
            Idioma
            <select name="idioma" [(ngModel)]="filtroIdioma">
              <option value="">Todos</option>
              <option value="es">Español</option>
              <option value="en">Inglés</option>
            </select>
          </label>
          <label>
            Busqueda
            <input
              name="q"
              [(ngModel)]="filtroBusqueda"
              placeholder="Nombre, número, correo o código"
            />
          </label>
          <button class="primary-button" type="submit">Filtrar</button>
        </form>
      </section>

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading">
            <div>
              <h3>Usuarios</h3>
              <span class="muted">{{ usuarios().length }} visibles</span>
            </div>
            @if (auth.isAdmin()) {
              <div>
                @if (nombresSaludoPendientes() !== null) {
                  <span class="muted">
                    {{ nombresSaludoPendientes() }} sin persistir en Cosmos
                  </span>
                }
                <button
                  type="button"
                  class="ghost-button"
                  [disabled]="completandoNombresSaludo() || nombresSaludoPendientes() === 0"
                  (click)="completarNombresSaludo()"
                >
                  {{
                    completandoNombresSaludo() ? 'Completando...' : 'Completar nombres de saludo'
                  }}
                </button>
              </div>
            }
          </div>
          <p class="muted">
            Revisa la columna Nombre para saludo. Si un valor no es correcto, usa Editar para
            ajustarlo sin cambiar el nombre completo.
          </p>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th scope="col">Código</th>
                  <th scope="col">Nombre</th>
                  <th scope="col">Nombre para saludo</th>
                  <th scope="col">Numero</th>
                  <th scope="col">Rol</th>
                  <th scope="col">Empresa</th>
                  <th scope="col">Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (usuario of usuarios(); track usuario.id) {
                  <tr>
                    <td>{{ usuario.codigoUsuarioLegible }}</td>
                    <td>{{ usuario.nombre }}</td>
                    <td>{{ usuario.nombreSaludo || 'Pendiente' }}</td>
                    <td>{{ usuario.whatsappNormalizado }}</td>
                    <td>{{ usuario.rol }}</td>
                    <td>{{ usuario.empresaId ?? usuario.empresa ?? '—' }}</td>
                    <td>
                      <span class="status-badge">{{ usuario.estado }}</span>
                    </td>
                    <td>
                      <button type="button" class="table-button" (click)="abrirFicha(usuario)">
                        Ver ficha
                      </button>
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
                    <td colspan="8" class="empty-cell">No hay usuarios para el filtro actual.</td>
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
            <label
              >Nombre completo
              <input name="nombre" [(ngModel)]="nuevoUsuario.nombre" required />
            </label>
            <label
              >Nombre para saludo
              <input
                name="nombreSaludo"
                [(ngModel)]="nuevoUsuario.nombreSaludo"
                placeholder="Se calcula si se deja vacío"
              />
            </label>
            <label>Numero <input name="numero" [(ngModel)]="nuevoUsuario.numero" required /></label>
            <label>
              Rol
              <select name="rolNuevo" [(ngModel)]="nuevoUsuario.rol">
                <option value="participante">Participante</option>
                <option value="admin">Admin</option>
                <option value="visor">Visor</option>
              </select>
            </label>
            <label
              >Correo <input name="email" type="email" [(ngModel)]="nuevoUsuario.email"
            /></label>
            <label>Empresa <input name="empresa" [(ngModel)]="nuevoUsuario.empresa" /></label>
            <label
              >ID empresa <input name="empresaId" [(ngModel)]="nuevoUsuario.empresaId"
            /></label>
            <label>Sede <input name="sede" [(ngModel)]="nuevoUsuario.sede" /></label>
            <label>Cargo <input name="cargo" [(ngModel)]="nuevoUsuario.cargo" /></label>
            <label>Area <input name="area" [(ngModel)]="nuevoUsuario.area" /></label>
            <label>
              Antiguedad (años)
              <input
                name="antiguedadAnios"
                type="number"
                step="0.000001"
                [(ngModel)]="nuevoUsuario.antiguedadAnios"
              />
            </label>
            <label>
              Idioma
              <select name="idiomaNuevo" [(ngModel)]="nuevoUsuario.idioma">
                <option value="es">Español</option>
                <option value="en">Inglés</option>
              </select>
            </label>
            <label>
              Usuario de WhatsApp
              <input name="usuarioWhatsapp" [(ngModel)]="nuevoUsuario.usuarioWhatsapp" />
            </label>
            <label
              >Tags <input name="tags" [(ngModel)]="tagsTexto" placeholder="t_area,t_empresa"
            /></label>
            <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
              {{ editandoId() ? 'Actualizar usuario' : 'Guardar usuario' }}
            </button>
          </form>
        </section>
      </div>

      @if (fichaUsuario(); as ficha) {
        <app-ficha-usuario-panel
          [usuario]="ficha"
          [historico]="historicoNumero()"
          [esAdmin]="auth.isAdmin()"
          (reasignar)="reasignarNumero($event)"
          (cerrar)="cerrarFicha()"
        />
      }

      <section class="panel">
        <div class="panel-heading">
          <h3>Tags</h3>
          <span class="muted">{{ tags().length }} visibles</span>
        </div>
        <form class="inline-form" (ngSubmit)="crearTag()">
          <label class="sr-only" for="tagNombre">Nombre de la etiqueta</label>
          <input
            id="tagNombre"
            name="tagNombre"
            [(ngModel)]="nuevoTag.nombre"
            placeholder="Nombre"
          />
          <label class="sr-only" for="tagTipo">Tipo de la etiqueta</label>
          <input id="tagTipo" name="tagTipo" [(ngModel)]="nuevoTag.tipoTag" placeholder="Tipo" />
          <label class="sr-only" for="tagDescripcion">Descripción de la etiqueta</label>
          <input
            id="tagDescripcion"
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

      @if (auth.isAdmin()) {
        <app-carga-masiva-panel
          [campanias]="campanias()"
          [reporte]="reporteCarga()"
          [cargando]="cargandoArchivo()"
          (cargar)="cargarArchivo($event)"
          (descargarPlantilla)="descargarPlantilla()"
        />
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsuariosPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  private readonly notificaciones = inject(NotificacionesService);
  protected readonly usuarios = signal<UsuarioAdmin[]>([]);
  protected readonly tags = signal<TagAdmin[]>([]);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly error = signal('');
  protected readonly editandoId = signal<string | null>(null);
  protected readonly fichaUsuario = signal<UsuarioAdmin | null>(null);
  protected readonly historicoNumero = signal<UsuarioAdmin[]>([]);
  protected readonly nombresSaludoPendientes = signal<number | null>(null);
  protected readonly completandoNombresSaludo = signal(false);

  protected filtroRol = '';
  protected filtroEstado = '';
  protected filtroBusqueda = '';
  protected filtroEmpresaId = '';
  protected filtroSede = '';
  protected filtroIdioma = '';
  protected tagsTexto = '';
  protected nuevoUsuario = formularioUsuarioVacio();
  protected nuevoTag = {
    nombre: '',
    tipoTag: '',
    descripcion: '',
  };

  protected readonly cargandoArchivo = signal(false);
  protected readonly reporteCarga = signal<ReporteCargaMasiva | null>(null);

  constructor() {
    this.load();
    if (this.auth.isAdmin()) {
      this.cargarNombresSaludoPendientes();
    }
  }

  completarNombresSaludo() {
    const pendientes = this.nombresSaludoPendientes();
    if (pendientes === 0 || this.completandoNombresSaludo()) {
      return;
    }

    const cantidad = pendientes === null ? '' : ` ${pendientes}`;
    if (
      !window.confirm(
        `Se agregará nombreSaludo a${cantidad} documento(s) que aún no lo tienen. ` +
          'Los valores ya existentes no se modificarán. ¿Deseas continuar?',
      )
    ) {
      return;
    }

    this.completandoNombresSaludo.set(true);
    this.api.completarNombresSaludo().subscribe({
      next: (resultado) => {
        this.completandoNombresSaludo.set(false);
        this.nombresSaludoPendientes.set(0);
        this.notificaciones.exito(
          `${resultado.completados} documento(s) de usuario actualizados en Cosmos.`,
        );
      },
      error: (err: unknown) => {
        this.completandoNombresSaludo.set(false);
        this.notificaciones.error(formatApiError(err));
      },
    });
  }

  private cargarNombresSaludoPendientes() {
    this.api.nombresSaludoPendientes().subscribe({
      next: (resultado) => this.nombresSaludoPendientes.set(resultado.pendientes),
      error: (err: unknown) => this.notificaciones.error(formatApiError(err)),
    });
  }

  load() {
    this.api
      .usuarios({
        rol: this.filtroRol,
        estado: this.filtroEstado,
        q: this.filtroBusqueda,
        empresaId: this.filtroEmpresaId,
        sede: this.filtroSede,
        idioma: this.filtroIdioma,
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
    this.api.campanias({ pageSize: 100 }).subscribe({
      next: (page) => this.campanias.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cargarArchivo(solicitud: SolicitudCargaMasiva) {
    this.cargandoArchivo.set(true);
    this.api
      .cargaMasivaUsuarios(solicitud.archivo, {
        campaniaId: solicitud.campaniaId || undefined,
        modo: solicitud.modo,
        resoluciones: solicitud.resoluciones,
      })
      .subscribe({
        next: (reporte) => {
          this.cargandoArchivo.set(false);
          this.reporteCarga.set(reporte);
          const conflictos = reporte.filas.filter((f) => f.motivo === 'conflicto_titular').length;
          const resumen =
            `Carga completada: ${reporte.creados} creados, ${reporte.actualizados} actualizados, ` +
            `${reporte.reasignados} reasignados, ${reporte.rechazados} rechazados.`;
          if (conflictos > 0) {
            // Un conflicto no es un fallo: es una decision que el admin debe tomar (I-08 §4.4).
            this.notificaciones.info(
              `${resumen} Hay ${conflictos} teléfono(s) que ya son de otra persona y esperan tu decisión.`,
            );
          } else {
            this.notificaciones.exito(resumen);
          }
          this.load();
        },
        error: (err: unknown) => {
          this.cargandoArchivo.set(false);
          this.notificaciones.error(formatApiError(err));
        },
      });
  }

  descargarPlantilla() {
    this.api.descargarPlantillaCarga().subscribe({
      next: (blob) => descargar(blob, 'plantilla_participantes_v1.xlsx'),
      error: (err: unknown) => this.notificaciones.error(formatApiError(err)),
    });
  }

  abrirFicha(usuario: UsuarioAdmin) {
    this.fichaUsuario.set(usuario);
    this.historicoNumero.set([]);
    this.api.usuariosPorNumero(usuario.whatsappNormalizado).subscribe({
      next: (historico) => this.historicoNumero.set(historico),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cerrarFicha() {
    this.fichaUsuario.set(null);
    this.historicoNumero.set([]);
  }

  reasignarNumero(formulario: FormularioReasignacion) {
    const usuario = this.fichaUsuario();
    if (!usuario) {
      return;
    }

    this.api
      .reasignarNumero(usuario.id, {
        nombre: formulario.nombre,
        email: formulario.email || null,
        empresaId: formulario.empresaId || null,
        sede: formulario.sede || null,
        cargo: formulario.cargo || null,
      })
      .subscribe({
        next: (resultado) => {
          this.notificaciones.exito(
            `Teléfono reasignado a ${resultado.usuario.nombre} (${resultado.usuario.codigoUsuarioLegible}). ` +
              'El titular anterior quedó inactivo conservando su historial.',
          );
          this.cerrarFicha();
          this.load();
        },
        error: (err: unknown) => this.notificaciones.error(formatApiError(err)),
      });
  }

  guardarUsuario() {
    const body = {
      ...this.nuevoUsuario,
      antiguedadAnios:
        this.nuevoUsuario.antiguedadAnios === '' ? null : Number(this.nuevoUsuario.antiguedadAnios),
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
      nombreSaludo: usuario.nombreSaludo,
      numero: usuario.whatsappNormalizado,
      rol: usuario.rol,
      area: usuario.area ?? '',
      empresa: usuario.empresa ?? '',
      empresaId: usuario.empresaId ?? '',
      sede: usuario.sede ?? '',
      cargo: usuario.cargo ?? '',
      email: usuario.email ?? '',
      antiguedadAnios: usuario.antiguedadAnios ?? '',
      idioma: usuario.idioma ?? 'es',
      usuarioWhatsapp: usuario.usuarioWhatsapp ?? '',
    };
    this.tagsTexto = (usuario.tags ?? []).join(',');
  }

  cancelarEdicion() {
    this.resetFormulario();
  }

  private resetFormulario() {
    this.editandoId.set(null);
    this.nuevoUsuario = formularioUsuarioVacio();
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

function formularioUsuarioVacio() {
  return {
    nombre: '',
    nombreSaludo: '',
    numero: '',
    rol: 'participante',
    area: '',
    empresa: '',
    empresaId: '',
    sede: '',
    cargo: '',
    email: '',
    antiguedadAnios: '' as number | string,
    idioma: 'es',
    usuarioWhatsapp: '',
  };
}

/** Dispara la descarga del blob con el nombre sugerido, sin dejar el object URL colgando. */
function descargar(blob: Blob, nombreArchivo: string) {
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombreArchivo;
  enlace.click();
  URL.revokeObjectURL(url);
}
