import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AccionConflictoTitular,
  Campania,
  ModoCargaMasiva,
  ReporteCargaMasiva,
  ResolucionConflictoTitular,
  ResultadoFilaCarga,
  UsuarioAdmin,
} from '../../core/api-models';

/** Lo que el panel de carga pide ejecutar al contenedor (I-08 v2 §4). */
export interface SolicitudCargaMasiva {
  archivo: File;
  modo: ModoCargaMasiva;
  campaniaId: string;
  resoluciones: ResolucionConflictoTitular[];
}

/** Datos del nuevo titular en una reasignacion manual (I-08 v2 §4.4). */
export interface FormularioReasignacion {
  nombre: string;
  email: string;
  empresaId: string;
  sede: string;
  cargo: string;
}

/** Traduce los motivos tipificados del backend a algo que un admin entienda sin documentacion. */
const MOTIVOS: Record<string, string> = {
  fila_incompleta: 'Falta el nombre o el teléfono',
  numero_invalido: 'El teléfono no es un número válido',
  email_invalido: 'El correo no tiene un formato válido',
  duplicado_en_archivo: 'Ese teléfono se repite en el archivo (se tomó la primera fila)',
  email_duplicado: 'Ese correo ya lo tiene otra persona activa',
  conflicto_titular: 'El teléfono ya es de otra persona',
  idioma_invalido: 'El idioma debe ser es o en',
  antiguedad_invalida: 'La antigüedad no es un número',
  no_encontrado: 'No existe nadie con ese teléfono (modo solo actualizar)',
  reasignacion_incompleta: 'La reasignación quedó a medias: revisa el número a mano',
};

export function describirMotivo(motivo?: string | null): string {
  if (!motivo) {
    return '—';
  }

  return MOTIVOS[motivo] ?? motivo;
}

@Component({
  selector: 'app-carga-masiva-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading">
      <h3>Carga masiva de participantes</h3>
      <button type="button" class="ghost-button" (click)="descargarPlantilla.emit()">
        Descargar plantilla vacía
      </button>
    </div>

    <p id="instrucciones-carga" class="muted">
      Usa la plantilla oficial de GHT (Excel o CSV), con estas columnas y en este orden:
      <code
        >Empresa · ID Empresa · Sede · Nombre · Cargo · Email · Antigüedad en la empresa en años ·
        Idioma · Telefono</code
      >. Solo <strong>Nombre</strong> y <strong>Telefono</strong> son obligatorios. Una fila con
      error no detiene el resto, y volver a subir el mismo archivo actualiza en vez de duplicar.
    </p>

    <form class="inline-form" (ngSubmit)="emitirCarga()">
      <label for="archivoCarga">Archivo de participantes (.xlsx o .csv)</label>
      <input
        id="archivoCarga"
        type="file"
        accept=".xlsx,.csv"
        aria-describedby="instrucciones-carga"
        (change)="seleccionarArchivo($event)"
      />
      <label>
        Modo
        <select name="modoCarga" [(ngModel)]="modo">
          <option value="upsert">Crear y actualizar</option>
          <option value="solo_actualizar">Solo actualizar los que ya existen</option>
        </select>
      </label>
      <label>
        Asociar a campaña (opcional)
        <select name="campaniaCarga" [(ngModel)]="campaniaId">
          <option value="">Sin asociar</option>
          @for (campania of campanias(); track campania.id) {
            <option [value]="campania.id">{{ campania.nombre }}</option>
          }
        </select>
      </label>
      <button class="primary-button" type="submit" [disabled]="!archivo() || cargando()">
        {{ cargando() ? 'Cargando…' : 'Cargar archivo' }}
      </button>
    </form>

    @if (modo === 'solo_actualizar') {
      <p class="muted" role="status">
        En este modo no se crea a nadie: las filas cuyo teléfono no exista se reportan como no
        encontradas.
      </p>
    }

    @if (reporte(); as r) {
      <p class="muted">
        Total: {{ r.totalFilas }} · Creados: {{ r.creados }} · Actualizados: {{ r.actualizados }} ·
        Reasignados: {{ r.reasignados }} · Rechazados: {{ r.rechazados }} · Asociados:
        {{ r.asociados }}
      </p>

      @if (conflictos().length) {
        <div class="panel-heading">
          <h4>Teléfonos que ya son de otra persona</h4>
        </div>
        <p class="muted" id="ayuda-conflictos">
          Estas filas no se guardaron. Decide qué hacer con cada una y vuelve a enviar
          <strong>el mismo archivo</strong>. Corregir el nombre mantiene a la misma persona;
          reasignar deja al titular anterior inactivo —conserva su historial— y crea una persona
          nueva con ese teléfono.
        </p>
        <div class="table-wrap">
          <table aria-describedby="ayuda-conflictos">
            <thead>
              <tr>
                <th scope="col">Fila</th>
                <th scope="col">Quién está registrado</th>
                <th scope="col">Quién trae el archivo</th>
                <th scope="col">Qué hacer</th>
              </tr>
            </thead>
            <tbody>
              @for (fila of conflictos(); track fila.fila) {
                <tr>
                  <td>{{ fila.fila }}</td>
                  <td>
                    {{ fila.nombreActual }}
                    <small class="muted">{{ codigoLegible(fila.codigoUsuarioAnterior) }}</small>
                  </td>
                  <td>{{ fila.nombrePropuesto }}</td>
                  <td>
                    <label class="sr-only" [attr.for]="'accion-' + fila.fila">
                      Qué hacer con la fila {{ fila.fila }}
                    </label>
                    <select
                      [id]="'accion-' + fila.fila"
                      [ngModel]="accionDe(fila.fila)"
                      [ngModelOptions]="{ standalone: true }"
                      (ngModelChange)="fijarAccion(fila.fila, $event)"
                    >
                      <option value="omitir">Dejarla sin cargar</option>
                      <option value="corregir_nombre">
                        Es la misma persona: corregir el nombre
                      </option>
                      <option value="reasignar">Es otra persona: reasignar el teléfono</option>
                    </select>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="actions-row">
          <button
            type="button"
            class="primary-button"
            [disabled]="!archivo() || cargando() || !hayDecisiones()"
            (click)="emitirCarga()"
          >
            Aplicar decisiones y volver a cargar
          </button>
          @if (!archivo()) {
            <span class="muted"
              >Vuelve a seleccionar el mismo archivo para aplicar las decisiones.</span
            >
          }
        </div>
      }

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th scope="col">Fila</th>
              <th scope="col">Resultado</th>
              <th scope="col">Código</th>
              <th scope="col">Detalle</th>
            </tr>
          </thead>
          <tbody>
            @for (fila of r.filas; track fila.fila) {
              <tr>
                <td>{{ fila.fila }}</td>
                <td>
                  <span class="status-badge">{{ fila.resultado }}</span>
                </td>
                <td>{{ codigoLegible(fila.codigoUsuario) }}</td>
                <td>{{ describir(fila.motivo) }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </section>`,
})
export class CargaMasivaPanel {
  readonly campanias = input.required<readonly Campania[]>();
  readonly reporte = input.required<ReporteCargaMasiva | null>();
  readonly cargando = input.required<boolean>();
  readonly cargar = output<SolicitudCargaMasiva>();
  readonly descargarPlantilla = output<void>();

  protected modo: ModoCargaMasiva = 'upsert';
  protected campaniaId = '';
  protected readonly archivo = signal<File | null>(null);
  private readonly decisiones = signal<Record<number, AccionConflictoTitular>>({});

  /** Filas que el backend devolvio como conflicto de titular; son las unicas resolubles. */
  protected readonly conflictos = computed<ResultadoFilaCarga[]>(
    () => this.reporte()?.filas.filter((fila) => fila.motivo === 'conflicto_titular') ?? [],
  );

  protected readonly hayDecisiones = computed(() =>
    Object.values(this.decisiones()).some((accion) => accion !== 'omitir'),
  );

  protected describir = describirMotivo;

  protected codigoLegible(codigo?: number | null): string {
    return codigo ? `U-${String(codigo).padStart(6, '0')}` : '—';
  }

  protected accionDe(fila: number): AccionConflictoTitular {
    return this.decisiones()[fila] ?? 'omitir';
  }

  protected fijarAccion(fila: number, accion: AccionConflictoTitular) {
    this.decisiones.update((actual) => ({ ...actual, [fila]: accion }));
  }

  protected seleccionarArchivo(evento: Event) {
    const input = evento.target as HTMLInputElement;
    this.archivo.set(input.files?.item(0) ?? null);
  }

  protected emitirCarga() {
    const archivo = this.archivo();
    if (!archivo) {
      return;
    }

    // Solo viajan las decisiones distintas de "omitir": omitir es no hacer nada.
    const resoluciones = Object.entries(this.decisiones())
      .filter(([, accion]) => accion !== 'omitir')
      .map(([fila, accion]) => ({ fila: Number(fila), accion }));

    this.cargar.emit({ archivo, modo: this.modo, campaniaId: this.campaniaId, resoluciones });
  }
}

@Component({
  selector: 'app-ficha-usuario-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading">
      <h3>Ficha de {{ usuario().nombre }}</h3>
      <button type="button" class="ghost-button" (click)="cerrar.emit()">Cerrar</button>
    </div>

    <dl class="detail-grid">
      <div>
        <dt>Código</dt>
        <dd>{{ usuario().codigoUsuarioLegible }}</dd>
      </div>
      <div>
        <dt>Teléfono</dt>
        <dd>{{ usuario().whatsappNormalizado }}</dd>
      </div>
      <div>
        <dt>Estado</dt>
        <dd>{{ usuario().estado }}</dd>
      </div>
      <div>
        <dt>Empresa</dt>
        <dd>{{ usuario().empresa ?? '—' }}</dd>
      </div>
      <div>
        <dt>ID empresa</dt>
        <dd>{{ usuario().empresaId ?? '—' }}</dd>
      </div>
      <div>
        <dt>Sede</dt>
        <dd>{{ usuario().sede ?? '—' }}</dd>
      </div>
      <div>
        <dt>Cargo</dt>
        <dd>{{ usuario().cargo ?? '—' }}</dd>
      </div>
      <div>
        <dt>Correo</dt>
        <dd>{{ usuario().email ?? '—' }}</dd>
      </div>
      <div>
        <dt>Antigüedad</dt>
        <dd>{{ usuario().antiguedadAnios ?? '—' }}</dd>
      </div>
      <div>
        <dt>Idioma</dt>
        <dd>{{ usuario().idioma }}</dd>
      </div>
      <div>
        <dt>Usuario de WhatsApp</dt>
        <dd>{{ usuario().usuarioWhatsapp ?? '—' }}</dd>
      </div>
    </dl>

    <h4>Historial de este teléfono</h4>
    <p class="muted" id="ayuda-historico">
      Un mismo teléfono puede haber pertenecido a varias personas. Solo una está activa; las demás
      conservan su historial de participación.
    </p>
    <div class="table-wrap">
      <table aria-describedby="ayuda-historico">
        <thead>
          <tr>
            <th scope="col">Código</th>
            <th scope="col">Nombre</th>
            <th scope="col">Estado</th>
            <th scope="col">Desde</th>
          </tr>
        </thead>
        <tbody>
          @for (titular of historico(); track titular.id) {
            <tr>
              <td>{{ titular.codigoUsuarioLegible }}</td>
              <td>{{ titular.nombre }}</td>
              <td>
                <span class="status-badge">{{ titular.estado }}</span>
              </td>
              <td>{{ titular.creadoEn }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4" class="empty-cell">Sin historial para este número.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>

    @if (esAdmin() && usuario().estado === 'activo') {
      <h4>Reasignar este teléfono a otra persona</h4>
      <p class="muted" id="ayuda-reasignar">
        {{ usuario().nombre }} quedará inactivo conservando su número y su historial, y se creará
        una persona nueva con este mismo teléfono. La nueva no hereda rol, etiquetas ni
        participaciones.
      </p>
      <form class="form-grid" aria-describedby="ayuda-reasignar" (ngSubmit)="confirmar()">
        <label
          >Nombre del nuevo titular <input name="reNombre" [(ngModel)]="formulario.nombre" required
        /></label>
        <label>Correo <input name="reEmail" [(ngModel)]="formulario.email" /></label>
        <label>ID empresa <input name="reEmpresaId" [(ngModel)]="formulario.empresaId" /></label>
        <label>Sede <input name="reSede" [(ngModel)]="formulario.sede" /></label>
        <label>Cargo <input name="reCargo" [(ngModel)]="formulario.cargo" /></label>
        <button class="danger-button" type="submit" [disabled]="!formulario.nombre.trim()">
          Reasignar teléfono
        </button>
      </form>
    }
  </section>`,
})
export class FichaUsuarioPanel {
  readonly usuario = input.required<UsuarioAdmin>();
  readonly historico = input.required<readonly UsuarioAdmin[]>();
  readonly esAdmin = input.required<boolean>();
  readonly reasignar = output<FormularioReasignacion>();
  readonly cerrar = output<void>();

  protected formulario: FormularioReasignacion = {
    nombre: '',
    email: '',
    empresaId: '',
    sede: '',
    cargo: '',
  };

  /**
   * El nombre del nuevo titular es obligatorio: la guarda vive aqui y no solo en el `disabled` del
   * boton, porque inactivar a alguien sin saber a quien se le entrega el numero seria peor que fallar.
   */
  protected confirmar() {
    if (!this.formulario.nombre.trim()) {
      return;
    }

    this.reasignar.emit({ ...this.formulario });
  }
}
