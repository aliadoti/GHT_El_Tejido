import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { CriterioRubrica, Rubrica } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';

type ModoRubrica = 'crear' | 'editar' | 'version';

interface FilaCriterio {
  id: string;
  nombre: string;
  descripcion: string;
  /** Se edita como porcentaje; el contrato viaja en fraccion (03 §3.11). */
  pesoPorcentaje: number;
}

/**
 * Editor estructurado de rubricas (11, DT-RUB-01 §4).
 *
 * La estructura es la fuente unica: aqui se autorizan escala, instrucciones y criterios ordenados, y
 * el `contenidoMarkdown` lo compila el servidor. El portal **nunca** lo envia como autoridad ni
 * mantiene un segundo compilador: el preview sale de `POST /api/admin/rubricas/prevalidar`, el mismo
 * validador y compilador que usa la escritura real.
 */
@Component({
  selector: 'app-rubricas-page',
  standalone: true,
  imports: [FormsModule, EstadoAccesibleComponent],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <h2>Rubricas</h2>
          <p class="help-text">
            Los criterios se administran aqui. En una campana o pregunta solo se selecciona una
            version completa.
          </p>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      <app-estado-accesible tipo="error" [mensaje]="error()" />

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading"><h3>Versiones</h3></div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Nombre</th>
                  <th>Version</th>
                  <th>Estado</th>
                  <th>Estructura</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (rubrica of rubricas(); track rubrica.id + rubrica.version) {
                  <tr>
                    <td>{{ rubrica.id }}</td>
                    <td>{{ rubrica.nombre }}</td>
                    <td>v{{ rubrica.version }}</td>
                    <td>
                      <span class="status-badge">{{ rubrica.estado }}</span>
                    </td>
                    <td>
                      <span class="status-badge">{{ textoIntegridad(rubrica) }}</span>
                    </td>
                    <td>
                      <button type="button" class="table-button" (click)="ver(rubrica)">Ver</button>
                      @if (auth.isAdmin()) {
                        <button type="button" class="table-button" (click)="editar(rubrica)">
                          {{ rubrica.estado === 'borrador' ? 'Editar' : 'Crear nueva version' }}
                        </button>
                        @if (rubrica.estado !== 'activa') {
                          <button
                            type="button"
                            class="table-button"
                            [disabled]="rubrica.integridadEstructural !== 'valida'"
                            (click)="cambiarEstado(rubrica, 'activa')"
                          >
                            Activar
                          </button>
                        }
                        @if (rubrica.estado === 'activa') {
                          <button
                            type="button"
                            class="table-button"
                            (click)="cambiarEstado(rubrica, 'archivada')"
                          >
                            Archivar
                          </button>
                        }
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="empty-cell">No hay rubricas.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        @if (vista(); as rubrica) {
          <section class="panel" aria-label="Vista de solo lectura de rubrica">
            <div class="panel-heading">
              <h3>Vista de rubrica</h3>
              <button type="button" class="ghost-button" (click)="cerrarVista()">Cerrar</button>
            </div>
            <dl class="detail-grid">
              <div>
                <dt>Nombre</dt>
                <dd>{{ rubrica.nombre }}</dd>
              </div>
              <div>
                <dt>Version</dt>
                <dd>v{{ rubrica.version }}</dd>
              </div>
              <div>
                <dt>Estado</dt>
                <dd>{{ rubrica.estado }}</dd>
              </div>
              <div>
                <dt>Escala</dt>
                <dd>{{ rubrica.escala.min }}–{{ rubrica.escala.max }}</dd>
              </div>
            </dl>
            <h4>Criterios</h4>
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>ID</th>
                    <th>Nombre</th>
                    <th>Descripcion</th>
                    <th>Peso</th>
                  </tr>
                </thead>
                <tbody>
                  @for (criterio of rubrica.criterios; track criterio.id) {
                    <tr>
                      <td>{{ criterio.orden }}</td>
                      <td>{{ criterio.id }}</td>
                      <td>{{ criterio.nombre }}</td>
                      <td>{{ criterio.descripcion }}</td>
                      <td>{{ porcentaje(criterio.peso) }}%</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <h4>Contenido Markdown (derivado)</h4>
            <p class="help-text">Lo genera el servidor desde la estructura; no se edita a mano.</p>
            <pre class="markdown-preview">{{ rubrica.contenidoMarkdown }}</pre>
          </section>
        } @else if (auth.isAdmin()) {
          <section class="panel">
            <div class="panel-heading">
              <h3>{{ tituloFormulario() }}</h3>
            </div>
            <form class="form-grid" (ngSubmit)="guardar()">
              <label
                >ID familia
                <input name="id" [(ngModel)]="form.id" [disabled]="modo() !== 'crear'" />
              </label>
              <label>Nombre <input name="nombre" [(ngModel)]="form.nombre" /></label>
              <label>Descripcion <input name="descripcion" [(ngModel)]="form.descripcion" /></label>
              <label>
                Instrucciones generales
                <textarea
                  name="instrucciones"
                  rows="3"
                  [(ngModel)]="form.instruccionesGenerales"
                ></textarea>
              </label>
              <div class="two-column">
                <label
                  >Escala minima
                  <input name="escalaMin" type="number" [(ngModel)]="form.escalaMin" />
                </label>
                <label
                  >Escala maxima
                  <input name="escalaMax" type="number" [(ngModel)]="form.escalaMax" />
                </label>
              </div>

              <h4>Criterios</h4>
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>ID</th>
                      <th>Nombre</th>
                      <th>Descripcion</th>
                      <th>Peso %</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (criterio of criterios(); track $index) {
                      <tr>
                        <td>{{ $index + 1 }}</td>
                        <td>
                          <input
                            [attr.aria-label]="'ID del criterio ' + ($index + 1)"
                            [ngModel]="criterio.id"
                            [ngModelOptions]="{ standalone: true }"
                            (ngModelChange)="cambiarCampo($index, 'id', $event)"
                          />
                        </td>
                        <td>
                          <input
                            [attr.aria-label]="'Nombre del criterio ' + ($index + 1)"
                            [ngModel]="criterio.nombre"
                            [ngModelOptions]="{ standalone: true }"
                            (ngModelChange)="cambiarCampo($index, 'nombre', $event)"
                          />
                        </td>
                        <td>
                          <input
                            [attr.aria-label]="'Descripcion del criterio ' + ($index + 1)"
                            [ngModel]="criterio.descripcion"
                            [ngModelOptions]="{ standalone: true }"
                            (ngModelChange)="cambiarCampo($index, 'descripcion', $event)"
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            [attr.aria-label]="'Peso del criterio ' + ($index + 1)"
                            [ngModel]="criterio.pesoPorcentaje"
                            [ngModelOptions]="{ standalone: true }"
                            (ngModelChange)="cambiarPeso($index, $event)"
                          />
                        </td>
                        <td>
                          <button
                            type="button"
                            class="table-button"
                            [disabled]="$index === 0"
                            (click)="mover($index, -1)"
                          >
                            Subir
                          </button>
                          <button
                            type="button"
                            class="table-button"
                            [disabled]="$index === criterios().length - 1"
                            (click)="mover($index, 1)"
                          >
                            Bajar
                          </button>
                          <button type="button" class="table-button" (click)="quitar($index)">
                            Quitar
                          </button>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="6" class="empty-cell">Agrega al menos un criterio.</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>

              <p [class]="sumaValida() ? 'help-text' : 'error-text'" data-test="suma-pesos">
                Suma de pesos: {{ sumaPesos() }}% (debe ser 100%)
              </p>

              <div class="form-actions">
                <button type="button" class="ghost-button" (click)="agregar()">
                  Agregar criterio
                </button>
                <button type="button" class="ghost-button" (click)="previsualizar()">
                  Revisar y previsualizar
                </button>
                <button class="primary-button" type="submit">{{ textoBoton() }}</button>
                @if (modo() !== 'crear') {
                  <button type="button" class="ghost-button" (click)="cancelar()">Cancelar</button>
                }
              </div>
            </form>

            @if (erroresValidacion().length > 0) {
              <div class="panel-heading"><h4>Revisar antes de guardar</h4></div>
              <ul data-test="errores-prevalidacion">
                @for (item of erroresValidacion(); track item.campo + item.motivo) {
                  <li>{{ describirError(item.campo, item.motivo) }}</li>
                }
              </ul>
            }

            @if (preview()) {
              <div class="panel-heading"><h4>Preview del servidor</h4></div>
              <p class="help-text">
                Lo compilo el servidor con la misma logica del guardado; no se edita a mano.
              </p>
              <pre class="markdown-preview" data-test="preview-markdown">{{ preview() }}</pre>
            }
          </section>
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RubricasPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  protected readonly rubricas = signal<Rubrica[]>([]);
  protected readonly vista = signal<Rubrica | null>(null);
  protected readonly error = signal('');
  protected readonly modo = signal<ModoRubrica>('crear');
  protected readonly criterios = signal<FilaCriterio[]>([]);
  protected readonly preview = signal('');
  protected readonly erroresValidacion = signal<Array<{ campo: string; motivo: string }>>([]);
  protected form = this.emptyForm();

  /** Se muestra en porcentaje para el administrador; el contrato viaja en fraccion. */
  protected readonly sumaPesos = computed(
    () =>
      Math.round(
        this.criterios().reduce((total, c) => total + (Number(c.pesoPorcentaje) || 0), 0) * 100,
      ) / 100,
  );

  protected readonly sumaValida = computed(() => Math.abs(this.sumaPesos() - 100) < 0.01);

  constructor() {
    this.load();
  }

  load() {
    this.api.rubricas({ pageSize: 50 }).subscribe({
      next: (page) => this.rubricas.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  tituloFormulario() {
    switch (this.modo()) {
      case 'editar':
        return 'Editar borrador';
      case 'version':
        return 'Nueva version';
      default:
        return 'Nueva rubrica';
    }
  }

  textoBoton() {
    switch (this.modo()) {
      case 'editar':
        return 'Guardar cambios';
      case 'version':
        return 'Crear version';
      default:
        return 'Crear v1';
    }
  }

  /**
   * Una version comprometida no se edita en sitio: se clona en un borrador nuevo (DT-RUB-01 §3.2),
   * de modo que la version activa y las evaluaciones que la usaron quedan intactas.
   */
  editar(rubrica: Rubrica) {
    this.error.set('');
    this.preview.set('');
    this.erroresValidacion.set([]);
    this.form = {
      id: rubrica.id,
      nombre: rubrica.nombre,
      descripcion: rubrica.descripcion,
      instruccionesGenerales: rubrica.instruccionesGenerales ?? '',
      escalaMin: rubrica.escala.min,
      escalaMax: rubrica.escala.max,
    };
    this.criterios.set(
      [...(rubrica.criterios ?? [])]
        .sort((a, b) => a.orden - b.orden)
        .map((c) => ({
          id: c.id,
          nombre: c.nombre,
          descripcion: c.descripcion ?? '',
          pesoPorcentaje: this.porcentaje(c.peso),
        })),
    );
    this.modo.set(rubrica.estado === 'borrador' ? 'editar' : 'version');
  }

  ver(rubrica: Rubrica) {
    this.vista.set(rubrica);
  }

  cerrarVista() {
    this.vista.set(null);
  }

  agregar() {
    this.criterios.update((filas) => [
      ...filas,
      { id: '', nombre: '', descripcion: '', pesoPorcentaje: 0 },
    ]);
  }

  quitar(indice: number) {
    this.criterios.update((filas) => filas.filter((_, i) => i !== indice));
  }

  mover(indice: number, delta: number) {
    const destino = indice + delta;
    this.criterios.update((filas) => {
      if (destino < 0 || destino >= filas.length) {
        return filas;
      }
      const copia = [...filas];
      [copia[indice], copia[destino]] = [copia[destino], copia[indice]];
      return copia;
    });
  }

  cambiarCampo(indice: number, campo: 'id' | 'nombre' | 'descripcion', valor: string) {
    this.criterios.update((filas) =>
      filas.map((fila, i) => (i === indice ? { ...fila, [campo]: valor } : fila)),
    );
  }

  cambiarPeso(indice: number, valor: number) {
    this.criterios.update((filas) =>
      filas.map((fila, i) => (i === indice ? { ...fila, pesoPorcentaje: Number(valor) } : fila)),
    );
  }

  /** Preview sin escritura: es el servidor quien valida y compila (04 §5.5). */
  previsualizar() {
    this.error.set('');
    this.api.prevalidarRubrica(this.payload()).subscribe({
      next: (resultado) => {
        this.erroresValidacion.set(resultado.errores ?? []);
        this.preview.set(resultado.valido ? resultado.contenidoMarkdown : '');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  guardar() {
    const cuerpo = this.payload();
    const peticion =
      this.modo() === 'editar'
        ? this.api.actualizarRubrica(this.form.id, { ...cuerpo, estado: 'borrador' })
        : this.modo() === 'version'
          ? this.api.crearVersionRubrica(this.form.id, { ...cuerpo, estado: 'borrador' })
          : this.api.crearRubrica({ ...cuerpo, estado: 'borrador' });

    peticion.subscribe({
      next: () => this.cancelar(),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cambiarEstado(rubrica: Rubrica, estado: string) {
    this.api.cambiarEstadoRubrica(rubrica.id, estado).subscribe({
      next: () => this.load(),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cancelar() {
    this.form = this.emptyForm();
    this.criterios.set([]);
    this.preview.set('');
    this.erroresValidacion.set([]);
    this.modo.set('crear');
    this.load();
  }

  textoIntegridad(rubrica: Rubrica) {
    switch (rubrica.integridadEstructural) {
      case 'valida':
        return 'verificada';
      case 'legacy_no_verificada':
        return 'sin verificar';
      default:
        return 'invalida';
    }
  }

  /** Traduce el motivo tipificado del servidor a lenguaje de administrador (04 §5.5). */
  describirError(campo: string, motivo: string) {
    const fila = /^criterios\.(\d+)\./.exec(campo);
    const donde = fila ? `Criterio ${Number(fila[1]) + 1}` : 'Rubrica';
    switch (motivo) {
      case 'requerido':
        return `${donde}: falta un dato obligatorio (${campo}).`;
      case 'duplicado':
        return `${donde}: ese valor ya lo usa otro criterio (${campo}).`;
      case 'formato_invalido':
        return `${donde}: el id solo admite minusculas, numeros y guion bajo.`;
      case 'fuera_de_rango':
        return `${donde}: el peso debe estar entre 0 y 100%.`;
      case 'no_consecutivo':
        return `${donde}: el orden de los criterios quedo incompleto.`;
      case 'suma_invalida':
        return 'Los pesos deben sumar 100%.';
      case 'invalida':
        return 'La escala debe tener un minimo menor que el maximo.';
      case 'limite_excedido':
        return 'Hay demasiados criterios para una sola version.';
      case 'integridad_invalida':
        return 'La estructura de esta version no esta verificada; crea una version nueva.';
      default:
        return `${donde}: ${campo} ${motivo}`;
    }
  }

  protected porcentaje(peso: number) {
    return Math.round(peso * 10000) / 100;
  }

  /**
   * Cuerpo canonico de 04 §5.5. **No** incluye `contenidoMarkdown`: el portal no envia la proyeccion
   * como autoridad. El orden viaja explicito segun la posicion de la tabla.
   */
  private payload() {
    return {
      id: this.form.id,
      nombre: this.form.nombre,
      descripcion: this.form.descripcion,
      instruccionesGenerales: this.form.instruccionesGenerales,
      escala: { min: Number(this.form.escalaMin), max: Number(this.form.escalaMax) },
      criterios: this.criterios().map((fila, indice) => ({
        id: fila.id,
        nombre: fila.nombre,
        descripcion: fila.descripcion,
        peso: Number(fila.pesoPorcentaje) / 100,
        orden: indice + 1,
      })) satisfies Array<Omit<CriterioRubrica, 'peso'> & { peso: number }>,
    };
  }

  private emptyForm() {
    return {
      id: '',
      nombre: '',
      descripcion: '',
      instruccionesGenerales: '',
      escalaMin: 1,
      escalaMax: 5,
    };
  }
}
